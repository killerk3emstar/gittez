#!/usr/bin/env bash
# Sprawdza założenia specu na żywym API GitHuba. Tylko odczyt, nie drukuje tokenu.
# Uruchom z katalogu głównego repo: ./tools/sanity-check.sh

cd "$(dirname "$0")/.." || exit 1
[ -f .env ] && set -a && . ./.env && set +a

if [ -z "$GITHUB_TOKEN" ]; then
  echo "Brak tokenu. Wklej go do .env jako GITHUB_TOKEN i uruchom ponownie."
  exit 1
fi

GH_LOGIN="${GH_LOGIN:-octocat}"
GH_LANG="${GH_LANG:-C#}"
API="https://api.github.com"
AUTH="Authorization: Bearer $GITHUB_TOKEN"
ACCEPT="Accept: application/vnd.github+json"
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

enc() { jq -rn --arg v "$1" '$v|@uri'; }
hr()  { printf '\n\033[1m%s\033[0m\n' "$1"; printf '%.0s-' {1..70}; echo; }
ok()  { printf '  \033[32m+\033[0m %s\n' "$1"; }
no()  { printf '  \033[31m!\033[0m %s\n' "$1"; }
warn(){ printf '  \033[33m?\033[0m %s\n' "$1"; }

# get <plik> <ścieżka>, zwraca czas odpowiedzi w ms
get() {
  curl -s -w '%{time_total}' -H "$AUTH" -H "$ACCEPT" -o "$1" "$API$2" \
    | awk '{printf "%d", $1*1000}'
}

PUSHED=$(date -u -v-90d +%Y-%m-%d 2>/dev/null || date -u -d '90 days ago' +%Y-%m-%d)

hr "0. Limity: czy token żyje i ile mamy budżetu"
get "$TMP/rl.json" "/rate_limit" >/dev/null
if ! jq -e '.resources' "$TMP/rl.json" >/dev/null 2>&1; then
  no "Token odrzucony: $(jq -r '.message // "?"' "$TMP/rl.json")"
  exit 1
fi
jq -r '.resources | to_entries[]
       | select(.key|IN("core","search","graphql"))
       | "  \(.key): \(.value.remaining)/\(.value.limit)"' "$TMP/rl.json"
CORE_LIMIT=$(jq -r '.resources.core.limit' "$TMP/rl.json")
[ "$CORE_LIMIT" -ge 5000 ] && ok "core = $CORE_LIMIT/h, zgodnie ze specem" \
                           || no "core = $CORE_LIMIT/h, spec zakłada 5000"

hr "1. Pasmo gwiazdek: czy suwak realnie zmienia zestaw wyników"
search_band() {
  local q="language:$GH_LANG good-first-issues:>=2 stars:$1..$2 pushed:>$PUSHED archived:false fork:false"
  get "$3" "/search/repositories?q=$(enc "$q")&per_page=50" >/dev/null
}
search_band 100  2500  "$TMP/band_small.json"
search_band 2000 50000 "$TMP/band_big.json"

for f in small big; do
  n=$(jq -r '.total_count // 0' "$TMP/band_$f.json")
  k=$(jq -r '.items|length' "$TMP/band_$f.json")
  echo "  pasmo $f: total_count=$n, pobrano=$k"
done
SMALL=$(jq -r '.items[].full_name' "$TMP/band_small.json" | sort)
BIG=$(jq -r '.items[].full_name' "$TMP/band_big.json" | sort)
OVERLAP=$(comm -12 <(echo "$SMALL") <(echo "$BIG") | wc -l | tr -d ' ')
NSMALL=$(echo "$SMALL" | grep -c .)
echo "  część wspólna obu pasm: $OVERLAP z $NSMALL"
if [ "$OVERLAP" -le 3 ]; then
  ok "Pasma zwracają rozłączne zestawy, suwak steruje wyszukiwaniem"
else
  warn "Duże pokrycie ($OVERLAP), suwak zmienia mniej niż zakłada spec"
fi
echo "  próbka z pasma 100..2500:"
jq -r '.items[:12][] | "    \(.full_name)  \(.stargazers_count) gwiazdek  \(.size)KB  topics:\(.topics|length)"' \
   "$TMP/band_small.json"

hr "2. Kwalifikator good-first-issues: czy cokolwiek odsiewa"
q_with="language:$GH_LANG good-first-issues:>=2 stars:100..2500 pushed:>$PUSHED archived:false fork:false"
q_without="language:$GH_LANG stars:100..2500 pushed:>$PUSHED archived:false fork:false"
get "$TMP/with.json" "/search/repositories?q=$(enc "$q_with")&per_page=1" >/dev/null
get "$TMP/without.json" "/search/repositories?q=$(enc "$q_without")&per_page=1" >/dev/null
W=$(jq -r '.total_count // -1' "$TMP/with.json")
WO=$(jq -r '.total_count // -1' "$TMP/without.json")
echo "  z kwalifikatorem:  $W repo"
echo "  bez kwalifikatora: $WO repo"
if [ "$W" -lt 0 ]; then
  no "Zapytanie odrzucone: $(jq -r '.message // "?"' "$TMP/with.json"), składnia >= nie działa"
elif [ "$W" -lt "$WO" ]; then
  ok "Kwalifikator odsiewa $((WO-W)) repo, składnia >= działa"
else
  warn "Brak różnicy, kwalifikator może być ignorowany"
fi

hr "3. Metadane: czy Topic Match i Complexity mają z czego żyć"
TOT=$(jq -r '.items|length' "$TMP/band_small.json")
NOTOP=$(jq -r '[.items[]|select((.topics|length)==0)]|length' "$TMP/band_small.json")
echo "  repo bez topics: $NOTOP z $TOT"
if [ "$NOTOP" -eq 0 ]; then
  warn "Zero repo bez topics, reguła null nigdy się nie odpali"
elif [ "$NOTOP" -lt $((TOT/2)) ]; then
  ok "$NOTOP repo trafi w regułę 'puste topics to null'"
else
  no "Ponad połowa bez topics, waga 25 dla Topic Match jest zła"
fi
jq -r '[.items[].size]|sort|"  rozmiar KB: min=\(.[0])  mediana=\(.[length/2|floor])  max=\(.[-1])"' \
   "$TMP/band_small.json"
jq -r '[.items[].stargazers_count]|sort|"  gwiazdki:   min=\(.[0])  mediana=\(.[length/2|floor])  max=\(.[-1])"' \
   "$TMP/band_small.json"

hr "4. Lejek: ilu z 25 finalistów ma realnie wolne issue"
FINALISTS=$(jq -r '.items[:25][].full_name' "$TMP/band_small.json")
SURV=0; TOTF=0
GFI=$(enc "good first issue")
for repo in $FINALISTS; do
  TOTF=$((TOTF+1))
  get "$TMP/iss.json" "/repos/$repo/issues?labels=$GFI&state=open&per_page=20" >/dev/null
  free=$(jq -r '[.[]?|select(has("pull_request")|not)|select((.assignees|length)==0)]|length' "$TMP/iss.json" 2>/dev/null || echo 0)
  [ "$free" -gt 0 ] && SURV=$((SURV+1))
  printf '    %-45s wolnych: %s\n' "$repo" "$free"
done
echo "  przeszło filtr: $SURV z $TOTF"
if [ "$SURV" -ge 10 ]; then
  ok "Zostaje $SURV, wystarczy na TOP 10"
else
  no "Zostaje tylko $SURV, podnieś liczbę finalistów albo zejdź na >=1"
fi

hr "5. Health Score: czy komponenty różnicują repozytoria"
printf '  %-38s %7s %9s %7s\n' "repo" "merge%" "mediana_h" "stale%"
CUT=$(date -u -v-90d +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d '90 days ago' +%Y-%m-%dT%H:%M:%SZ)
for repo in $(echo "$FINALISTS" | head -8); do
  get "$TMP/pr.json"   "/repos/$repo/pulls?state=all&per_page=30&sort=updated" >/dev/null
  get "$TMP/prop.json" "/repos/$repo/pulls?state=open&sort=created&direction=asc&per_page=100" >/dev/null

  read -r MERGE MED <<<"$(jq -r '
    [.[]?|select(.draft==false)|select(.closed_at!=null)] as $r
    | if ($r|length) < 5 then "null null"
      else
        ([$r[]|select(.merged_at!=null)]|length) as $m
        | ([$r[]|((((.merged_at // .closed_at)|fromdate) - (.created_at|fromdate))/3600)]|sort) as $h
        | "\((($m/($r|length))*100)|floor) \(($h[($h|length)/2|floor])|floor)"
      end' "$TMP/pr.json" 2>/dev/null || echo "null null")"

  STALE=$(jq -r --arg cut "$CUT" '
    (.|length) as $n
    | if $n==0 then "null"
      else "\((([.[]|select(.created_at < $cut)]|length)/$n*100)|floor)" end' "$TMP/prop.json" 2>/dev/null || echo "null")

  printf '  %-38s %7s %9s %7s\n' "$repo" "$MERGE" "$MED" "$STALE"
done
echo
echo "  Jeśli wszystkie merge% mieszczą się w 10 punktach, Health nie różnicuje"
echo "  i trzeba przeważyć komponenty."

hr "6. Profil: języki własne (REST) i z kontrybucji (GraphQL)"
get "$TMP/repos.json" "/users/$GH_LOGIN/repos?per_page=100&sort=pushed" >/dev/null
if jq -e 'type=="array"' "$TMP/repos.json" >/dev/null 2>&1; then
  echo "  własne repozytoria bez forków, top języki:"
  jq -r '[.[]|select(.fork==false)] | group_by(.language)[]
         | select(.[0].language!=null)
         | "    \(.[0].language): \(length) repo"' "$TMP/repos.json" | sort -t: -k2 -rn | head -6
  jq -r '[.[]|select(.fork==false)|.size]|sort|"    mediana rozmiaru: \(.[length/2|floor]) KB"' "$TMP/repos.json"
  ok "REST zwraca profil jednym wywołaniem"
else
  no "Profil nie wrócił: $(jq -r '.message // "?"' "$TMP/repos.json")"
fi

GQL=$(jq -nc --arg l "$GH_LOGIN" '{query:"query($l:String!){user(login:$l){repositoriesContributedTo(first:100,contributionTypes:[COMMIT,PULL_REQUEST],includeUserRepositories:false){totalCount nodes{nameWithOwner primaryLanguage{name}}}}}",variables:{l:$l}}')
curl -s -H "$AUTH" -H "Content-Type: application/json" -d "$GQL" "$API/graphql" > "$TMP/gql.json"
if jq -e '.data.user.repositoriesContributedTo' "$TMP/gql.json" >/dev/null 2>&1; then
  CNT=$(jq -r '.data.user.repositoriesContributedTo.totalCount' "$TMP/gql.json")
  ok "GraphQL repositoriesContributedTo działa, $CNT repo z kontrybucjami"
  jq -r '.data.user.repositoriesContributedTo.nodes[]?|select(.primaryLanguage!=null)
         | "    \(.nameWithOwner) [\(.primaryLanguage.name)]"' "$TMP/gql.json" | head -8
else
  no "GraphQL: $(jq -r '.errors[0].message // .message // "?"' "$TMP/gql.json")"
fi

hr "7. Latencja: czy założenie 250 ms na wywołanie się broni"
SUM=0
for i in 1 2 3 4 5; do
  T=$(get "$TMP/lat.json" "/repos/dotnet/maui")
  SUM=$((SUM+T))
done
AVG=$((SUM/5))
echo "  średnia z 5 wywołań: ${AVG} ms"
echo "  100 wywołań sekwencyjnie:   $((100*AVG/1000)) s"
echo "  100 wywołań przy 8 wątkach: $((100*AVG/8000)) s"
if [ "$AVG" -le 400 ]; then
  ok "Założenie 250 ms się broni, semafor 8 wystarczy"
else
  warn "Wolniej niż zakłada spec, sprawdź pulowanie połączeń w kliencie"
fi

hr "Podsumowanie"
echo "  Sekcje 1, 4 i 5 decydują o tym, czy scoring ma sens."
echo "  Zużycie limitu sprawdź nagłówkiem x-ratelimit-used, endpoint /rate_limit bywa nieaktualny."
