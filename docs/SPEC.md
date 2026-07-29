# Gittez: specyfikacja implementacyjna

> Zadanie rekrutacyjne CetusPro 2026, ścieżka **full stack**.
> Deadline zgłoszenia: **29 lipca**. Czas realizacji: ~1,5 dnia (niefortunny termin rodzinnego wyjazdu bez laptopa).
> Ten dokument jest źródłem prawdy dla implementacji. Wszystkie wzory operują
> wyłącznie na polach, które GitHub API faktycznie zwraca.
>
> **Rewizja 2 (28 lipca, po weryfikacji na realnych danych).** Kierunek
> wyszukiwania i część wag scoringu zmieniły się po odpaleniu prawdziwych
> zapytań, patrz §0.

## Status tego dokumentu

Spec jest zachowany w postaci, w jakiej powstał przed kodem, razem z trybem
rozkazującym („nie podnosić", „przeczytać przed pisaniem kodu") i z listą cięć na
wypadek braku czasu. Nie jest przepisywany pod stan końcowy, bo jego wartością
jest właśnie to, że pokazuje, co było wiadomo przed implementacją i co pomiar
zmienił. Wagi, progi i kształt pipeline'u z §5 i §6 zgadzają się z kodem.

Cztery rzeczy wyszły w praktyce inaczej, niż zakłada tekst poniżej:

| Miejsce | Spec zakładał | Wyszło |
|---------|---------------|--------|
| §2.2 | 6-8 s na pierwszy przebieg | 18,9 s lokalnie, 20,9 s na wdrożeniu, zimny cache, `limit=3` |
| §7 | endpoint `GET /api/repos/{owner}/{name}` | niepotrzebny, modal składa się z rozbicia przychodzącego z `/api/recommendations` |
| §10 | ~22 przypadki testowe | 51 metod w 7 plikach, doszły testy degradacji |
| §11 | Railway plus Vercel, CORS pod dwie domeny | oba obrazy na Railwayu, jeden origin, CORS niepotrzebny |

---

## 0. Co zmieniła weryfikacja na danych

Przed napisaniem kodu odpaliłem docelowe zapytanie `curl`em i przeczytałem
wyniki. Trzy założenia okazały się błędne. Zapis zostaje w specu, bo uzasadnia
kształt pipeline'u i wchodzi do README.

**0.1 Wyszukiwanie issues selekcjonuje spam.** Zapytanie
`label:"good first issue" state:open no:assignee language:C# archived:false`
z `sort=updated&order=desc` zwróciło na 100 trafień około dziesięciu realnych
projektów. Reszta to repozytoria z wygenerowanym backlogiem issues,
`VibecodingGermany/Project_Nova`, `stormairfly/StormATC` (cztery issues typu
„Add a fictional IFR clearance readback test fixture"),
`SSC-STUDIO/UniversalDeviceToolkit-Plugins` (pięć issues, każde z prefiksem
`[Good First Issue]`). Przyczyna jest w parametrze sortowania: `updated desc`
premiuje repo ruszane najczęściej, a repo z automatycznie generowanym backlogiem
jest ruszane bez przerwy.

**0.2 Liczba pasujących issues jest odwrotnie skorelowana z jakością.**
Rozkład trafień per repozytorium:

```
12  jchable/okf4net                           ← projekt nieznany
 5  SSC-STUDIO/UniversalDeviceToolkit-Plugins ← generowany backlog
 5  eesast/dotnet-workshop                    ← repo na zajęcia
 4  stormairfly/StormATC                      ← generowany backlog
...
 2  MudBlazor/MudBlazor
 2  dotnet/maui
 1  unoplatform/uno
```

Dojrzały projekt ma w danej chwili jedno albo dwa wolne good first issues, bo ludzie
je rozbierają. Repo, którego nikt nie zna, ma dwanaście. **Konsekwencja: liczby
issues nie wolno używać ani do pre-rankingu kandydatów, ani jako komponentu
punktowanego rosnąco.**

Pierwsza wersja poprawki obniżała Issue Freshness z 15 pkt do 5 i czyniła go
binarnym. To było pół kroku. Ostatecznie **komponent wyleciał całkowicie**,
z dwóch powodów:

- Krok 5 pipeline'u i tak odrzuca repozytoria bez wolnego issue, więc każdy
  kandydat, który dożywa rankingu, dostawałby 5/5. Komponent dający wszystkim
  tyle samo jest w interfejsie opartym na wytłumaczalności balastem, dziesięć
  kart z identycznym paskiem.
- **Kolejność się nie spinała.** Match liczymy w kroku 4 dla ~100 kandydatów z
  metadanych z search. Dane o przypisaniu issues są dostępne dopiero po kroku 5,
  czyli tylko dla finalistów. Komponent nie miałby z czego powstać bez liczenia
  Match dwa razy.

Fakt istnienia wolnego issue jest **filtrem, nie punktami**, i tak jest
komunikowany w UI: „wszystkie wyniki mają co najmniej jedno nieprzypisane issue".
Pięć punktów przeszło do Complexity Match.

**0.3 Próg gwiazdek jest tańszy, niż zakładałem.** Pierwotne ograniczenie
brzmiało „odsianie repo poniżej 50 gwiazdek odcina wartościowe małe projekty".
Dane pokazują, że w puli `good first issue` poniżej tego progu wartościowych
małych projektów prawie nie ma. Jest tam generowany spam. Próg zostaje jako twardy
filtr i idzie w górę do **100**.

**0.4 `sort=stars` zabija dwa komponenty scoringu.** Po przejściu na wyszukiwanie
repozytoriów (§0.1) drugi przebieg zwrócił 30 realnych projektów, zero spamu,
`topics` obecne w 29 z 30. Ale rozstrzał gwiazdek to 5 542-55 079, a lista jest
zdominowana przez `dotnet/*` (9 pozycji). Konsekwencje dla Match Score:

```
Community Fit (target=500):  msbuild 5.5k ⭐ → 2.3 pkt
                             MudBlazor 10k ⭐ → 0.6 pkt
                             jellyfin 55k ⭐  → 0.0 pkt      → 20 pkt martwych
Complexity Match:            repo 100-1000× większe od mediany studenta
                                                            → 5 pkt dla wszystkich
Language Match:              język jest kluczem zapytania    → 30/30 dla wszystkich
```

Trzy z pięciu komponentów stałe. Realny zakres różnicowania: 30 pkt ze stu.

Przyczyna jest ta sama co w §0.1: **parametr sortowania walczy ze scoringiem**.
`sort=stars` bierze wyłącznie czubek rozkładu i wycina zakres wielkości, dla
którego Community Fit w ogóle powstał.

**Naprawa:** `targetStars` wchodzi do **zapytania** jako pasmo, a nie tylko do
scoringu, i rezygnujemy z sortowania po gwiazdkach (§5 krok 2). Suwak wielkości
projektu przestaje być ozdobnikiem i staje się realnym sterowaniem
wyszukiwaniem, przesunięcie go zwraca inny zestaw repozytoriów, a nie te same
karty z innymi liczbami.

**0.5 Pełna kalibracja na żywym API** (`calibrate.py` w repo). Przed kodowaniem
przepuściłem cały pipeline przez prawdziwe dane dla dwóch zestawów językowych i
zmierzyłem **rozstrzał każdego komponentu**, bo komponent, który wszystkim daje
podobnie, jest w interfejsie opartym na wytłumaczalności bezwartościowy.

Pula: 300 kandydatów (C#/TS/Python) i 257 (TS/Swift/Python), pasmo `100..2500`.

| Komponent | rozstrzał | werdykt |
|-----------|-----------|---------|
| Complexity **ratio** (obecny wzór) | med = 6/25, czyli **na podłodze** | zepsuty |
| Complexity **percentyl** (propozycja) | 0 → 25, mediana 12,5 | naprawiony |
| Topic Match | 0 → 25 | OK |
| Community Fit | 7,6 → 20 | OK, pasmo zadziałało |
| Language Match | 24 → 30 | słaby z definicji (język jest kluczem zapytania) |
| Merge Rate | 0 → 25 | najlepszy w Health |
| Latency, **stare progi** | 20 → 25 | prawie martwy |
| Latency, **nowe progi** | 6 → 25 | naprawiony |
| Stale Ratio | 0 → 20 | OK |
| Issue Turnaround | 6 → 15 | OK |

Cztery konkretne konsekwencje, wszystkie wpisane niżej w spec:

1. **Complexity Match idzie na percentyl** (§6.1). Mediana rozmiaru repo w puli
   to 44 MB, mediana moich własnych projektów 337 KB, `ratio` zawsze wychodzi
   poza pasmo i **ponad połowa kandydatów dostaje minimum**. Porównywanie
   projektu studenta z produkcyjną bazą kodu zawsze zwraca „dużo większy".
2. **Progi Resolution Latency w dół** (§6.2). Zmierzone mediany: p25 = 1 h,
   p50 = 2-3 h, p75 = 17-24 h, max = 199 h. Próg „≤ 48 h → komplet" dawał
   maksimum niemal wszystkim.
3. **Ściąganie wyniku przy brakujących komponentach, sprawdzone i ODRZUCONE.**
   Bałem się, że `null` + procentowanie premiuje repozytoria bez danych. Zmierzone:
   komplet komponentów ma 20 z 25 finalistów, mediana pewności 100 %, a ściągnięcie
   wyniku do neutralnych 50 **nie zmieniło ani jednej pozycji w TOP 10**.
   Mechanizm nie wchodzi, nie dokładamy złożoności, która nic nie zmienia.
4. **Wynik zbiorczy nie może być bohaterem karty** (§9). Komponenty różnicują
   świetnie, ale `finalScore` w widocznej dziesiątce mieści się w **6,8 punktu**
   (81,4-88,2). To normalne dla czubka każdego rankingu, ale wielka liczba na
   karcie sugeruje precyzję, której tam nie ma.

**Wniosek architektoniczny:** krok 2 pipeline'u zmienia kierunek z wyszukiwania
issues na wyszukiwanie repozytoriów (§5), co przy okazji likwiduje 30 wywołań
per przebieg i problem jajka i kury przy filtrach. Sortowanie po gwiazdkach nie
wchodzi.

---

## 1. Cel i zakres

**One-liner:** GitHub ma miliony repozytoriów. Gittez pokazuje te kilka, do
których warto zacząć kontrybutować w tym tygodniu.

**Problem:** nie brakuje issues z labelem `good first issue`. Brakuje filtra
jakości, które repo jest żywe, maintainer responsywny, issue niezgniłe,
technologia moja. Od czasu, gdy generowanie repozytoriów stało się tanie, doszedł
drugi problem: odsianie projektów, które tylko wyglądają na żywe.

**Użytkownik:** programista (student / junior / mid), który chce wejść w OSS albo
znaleźć kolejny projekt, i nie chce spędzić wieczoru na klikaniu w martwe
repozytoria.

### 1.1 Zakres MVP (musi działać)

| # | Funkcja | Ścieżka wymagania CetusPro |
|---|---------|----------------------------|
| 1 | Login GitHub → analiza profilu publicznego → wykryte języki jako edytowalne chipy | przepływ: lista |
| 2 | Lista 10 rekomendacji z Match Score i Health Score | przepływ: lista |
| 3 | Rozbicie obu score'ów + lista issues (modal wystarczy) | przepływ: szczegóły |
| 4 | Watchlist, dodanie repo, **edycja własnej notatki**, usunięcie | przepływ: tworzenie/edycja + zapis do bazy |
| 5 | Cache w Postgresie z TTL i ETagami | architektura |
| 6 | Stany UI: ładowanie / błąd / brak wyników / limit API | wymóg wprost |
| 7 | Dokumentacja API (Scalar) + migracje EF Core | wymóg wprost |
| 8 | `docker compose up` uruchamia całość | uruchamialność |
| 9 | Testy jednostkowe ScoringService | testowanie |

### 1.2 Świadomie POZA zakresem

Wpisać do README z uzasadnieniem, bo to jest punktowane wyżej niż niedokończone funkcje.

- **OAuth / logowanie**: dane są publiczne, token użytkownika nie jest potrzebny.
  OAuth to tarcie w demo: recenzent musiałby autoryzować obcą aplikację na swoim
  koncie. Tożsamość watchlisty = anonimowy UUID sesji w `localStorage`.
- Powiadomienia e-mail o nowych issues.
- Background service prewarmujący cache: zastąpiony seed dumpem w repo.
- Ranking oparty na ML/LLM: scoring jest jawny i wytłumaczalny z założenia.
- Wyszukiwanie semantyczne (`search_type=hybrid`): istnieje, ale limit 10 req/min
  i osobna kalibracja poza budżetem czasowym.

---

## 2. Architektura

```
┌──────────────────────────┐      ┌──────────────────────┐
│  web (React 19 + Vite)   │─────▶│  api (.NET 10)       │
│  nginx :8080             │ REST │  :8080               │
└──────────────────────────┘      └──────────┬───────────┘
                                             │
                            ┌────────────────┴────────────────┐
                            ▼                                 ▼
                   ┌─────────────────┐             ┌────────────────────┐
                   │  PostgreSQL 16  │             │  GitHub API        │
                   │  cache + zapis  │             │  REST + GraphQL    │
                   └─────────────────┘             └────────────────────┘
```

**Frontend komunikuje się z backendem wyłącznie przez REST.** Nie wolno wołać
`api.github.com` z przeglądarki, inaczej token wycieka i limit jest per-IP
użytkownika.

**Wersje: .NET 10 (LTS, wsparcie do listopada 2028) i React 19.** .NET 9 jest w
fazie maintenance z końcem wsparcia 10 listopada 2026, zakładanie nowego
projektu na wersji STS cztery miesiące przed EOL nie ma uzasadnienia, a koszt
zmiany to jedna linijka `TargetFramework`.

### 2.1 Struktura solucji

```
/
├── docker-compose.yml
├── .env.example
├── README.md
├── db/seed/repo_cache_seed.sql       # ładowany PRZEZ API po migracjach, nie przez initdb
├── src/
│   ├── Gittez.Api/                # ASP.NET Core, endpointy, DI, Scalar
│   │   ├── Endpoints/                # minimal API, pogrupowane
│   │   ├── Contracts/                # DTO request/response
│   │   └── Program.cs
│   ├── Gittez.Core/               # logika, BEZ zależności od ASP.NET i EF
│   │   ├── Scoring/                  # czyste funkcje, tu żyją testy
│   │   ├── Models/                   # modele domenowe
│   │   └── Abstractions/             # IGitHubClient, ILanguageSource, IRepoCache
│   ├── Gittez.Infrastructure/     # EF Core, Octokit, GraphQL, implementacje
│   │   ├── Persistence/              # DbContext, Migrations, Seeder
│   │   └── GitHub/
│   └── Gittez.Tests/              # xUnit
└── web/                              # Vite + React 19 + TS
```

**Dlaczego taki podział:** `Gittez.Core` nie referencuje niczego poza BCL.
Scoring to czyste funkcje bez I/O, testy jednostkowe uruchamiają się w
milisekundach, bez bazy i bez mocka HTTP.

### 2.2 Współbieżność: decyzja architektoniczna

Jeden przebieg to **~100 wywołań HTTP** (§4.3), liczba zmierzona, nie
szacowana. To, a nie limit zapytań, jest realnym wąskim gardłem.

Zmierzone na żywym API (`calibrate.py`): pojedyncze wywołanie sekwencyjne
~530 ms, ale **równolegle latencja rośnie do 1,1-1,6 s**, bo GitHub dławi
współbieżność. Faza równoległa zajęła ~14-15 s. I to **niezależnie od tego, czy
puściłem 8, czy 16 wątków**. Zwiększanie współbieżności nic nie daje.

Pomiar był robiony bez pulowania połączeń (każde żądanie to nowy handshake TLS),
więc `HttpClient` z `SocketsHttpHandler` powinien zejść wyraźnie niżej.
**Realistyczne założenie dla .NET: 6-8 sekund na pierwszy przebieg**, nie 3.

Konsekwencje:
- `Parallel.ForEachAsync` z `MaxDegreeOfParallelism = 8`. Nie podnosić, zmierzone,
  że nie pomaga, a GitHub ma wtórne limity kończące się 403 z `Retry-After`,
  którego nie widać w `X-RateLimit-Remaining`.
- **Skeleton nie jest ozdobnikiem, tylko wymogiem**: siedem sekund pustego
  ekranu to porzucona sesja.
- Opcjonalnie, jeśli starczy czasu: oddaj listę posortowaną po Match od razu, a
  plakietki Health dociągaj progresywnie. Match nie kosztuje ani jednego
  wywołania poza wyszukiwaniem.

---

## 3. Model danych

EF Core 10 + Npgsql. Migracje w repo, `dotnet ef migrations add Initial`.

```sql
-- profil wyliczony z publicznych repozytoriów i kontrybucji użytkownika
profiles (
  github_login      VARCHAR(64) PRIMARY KEY,
  top_languages     JSONB NOT NULL,      -- [{"name":"C#","ownedRepos":7,"contributedRepos":2,"bytesShare":0.41}]
  median_size_kb    INTEGER NOT NULL,
  interests         JSONB NOT NULL,      -- ["swift","embedded","esp32"]
  public_repo_count INTEGER NOT NULL,
  computed_at       TIMESTAMPTZ NOT NULL
);

-- cache metadanych repo + policzony Health Score
repo_cache (
  full_name          VARCHAR(255) PRIMARY KEY,   -- "owner/name"
  data               JSONB NOT NULL,             -- znormalizowany snapshot
  etag               VARCHAR(128),               -- do conditional requests
  fetched_at         TIMESTAMPTZ NOT NULL,
  health_score       NUMERIC(5,2),               -- NULL = jeszcze nie liczony
  health_breakdown   JSONB,
  health_computed_at TIMESTAMPTZ
);
CREATE INDEX ix_repo_cache_fetched ON repo_cache (fetched_at);

-- cache issues; kolumny wyciągnięte z JSONB do szybkiego filtrowania
issue_cache (
  id               BIGINT PRIMARY KEY,           -- id issue z GitHuba
  repo_full_name   VARCHAR(255) NOT NULL,
  number           INTEGER NOT NULL,
  title            TEXT NOT NULL,
  html_url         TEXT NOT NULL,
  labels           JSONB NOT NULL,
  comment_count    INTEGER NOT NULL,
  body_length      INTEGER NOT NULL,
  has_assignee     BOOLEAN NOT NULL,             -- z tablicy assignees, patrz §4.4
  difficulty       SMALLINT NOT NULL,            -- 1..3, heurystyka
  issue_created_at TIMESTAMPTZ NOT NULL,
  issue_updated_at TIMESTAMPTZ NOT NULL,
  fetched_at       TIMESTAMPTZ NOT NULL
);
CREATE INDEX ix_issue_cache_repo ON issue_cache (repo_full_name);

-- anonimowa sesja zamiast konta użytkownika
sessions (
  id           UUID PRIMARY KEY,
  created_at   TIMESTAMPTZ NOT NULL,
  last_seen_at TIMESTAMPTZ NOT NULL
);

-- ścieżka zapisu i edycji
watchlist_items (
  id             BIGSERIAL PRIMARY KEY,
  session_id     UUID NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  repo_full_name VARCHAR(255) NOT NULL,
  note           TEXT,                            -- edytowalne przez użytkownika
  created_at     TIMESTAMPTZ NOT NULL,
  updated_at     TIMESTAMPTZ NOT NULL,
  UNIQUE (session_id, repo_full_name)
);
CREATE INDEX ix_watchlist_session ON watchlist_items (session_id);
```

**Sesja jest tworzona leniwie.** Pierwszy `POST /api/watchlist` z nieznanym
`X-Session-Id` wstawia wiersz do `sessions` przed insertem pozycji, inaczej
poleci naruszenie klucza obcego.

---

## 4. Integracja z GitHub API: co faktycznie dostajemy

Autoryzacja: **jeden serwerowy PAT** w zmiennej `GITHUB_TOKEN` (scope: brak /
`public_repo`).

### 4.1 Trzy niezależne pule limitów

| Pula | Z tokenem | Bez tokenu | Co ją zużywa |
|------|-----------|------------|--------------|
| **Core** | 5 000 / godzinę | 60 / godzinę | `/repos/...`, `/pulls`, `/issues`, `/users` |
| **Search** | 30 / **minutę** | 10 / minutę | wyłącznie `/search/*` |
| **GraphQL** | 5 000 punktów / godzinę | - | `/graphql`, koszt od liczby węzłów |

**Bez tokenu nie da się wykonać ani jednego świeżego przebiegu**, pula core
daje 60 zapytań na godzinę, a jeden przebieg zużywa ich około stu (§4.3).
Aplikacja działa wtedy wyłącznie na danych z seeda. I tak, i tylko tak, wolno to
opisać w README.

### 4.2 Wykorzystywane endpointy

| Endpoint | Pula | Koszt | Co z niego bierzemy |
|----------|------|-------|---------------------|
| `GET /users/{login}/repos?per_page=100&sort=pushed` | core | 1 | `language`, `size`, `topics`, `fork`, profil z własnych repo |
| `POST /graphql` → `repositoriesContributedTo` | graphql | ~11 pkt | języki i bajty z repo, do których użytkownik kontrybutował |
| `GET /search/repositories?q=...` | search | 1 na język | **pełne obiekty repo**: `stargazers_count`, `topics`, `size`, `pushed_at`, `language`, `license`, `archived` |
| `GET /repos/{o}/{n}/issues?labels=good first issue&state=open&per_page=20` | core | 1 na finalistę | realne issues + `assignees` + daty |
| `GET /repos/{o}/{n}/pulls?state=all&per_page=30&sort=updated` | core | 1 na finalistę | merge rate + resolution latency |
| `GET /repos/{o}/{n}/pulls?state=open&sort=created&direction=asc&per_page=100` | core | 1 na finalistę | stale ratio |
| `GET /repos/{o}/{n}/issues?state=closed&per_page=30` | core | 1 na finalistę | issue turnaround |

**Czego już NIE wołamy:** `GET /repos/{owner}/{name}` per kandydat. Repo search
zwraca komplet metadanych, więc 30 wywołań znika z budżetu. `pushed_at` z tego
samego źródła daje Commit Velocity za darmo.

### 4.3 Budżet wywołań na jeden świeży przebieg

Health liczymy dopiero dla finalistów, **którzy przeszli filtr wolnych issues**
(krok 6). Liczby zmierzone na dwóch pełnych przebiegach (§0.5), filtr przepuścił
24/25 i 25/25, więc odsiew jest znacznie mniejszy, niż zakładałem.

```
                                              zmierzone   pula
  profil: własne repozytoria                          1   core
  profil: kontrybucje (GraphQL)                       1   graphql
  repo search (po jednym na język)                    3   search
  issues dla 25 finalistów                           25   core
  health dla ~24 ocalałych (3 × 24)                  72   core
                                              ─────────
  RAZEM                                         ~101-104
  w tym pula core                                98 (96%)
```

Kontrola: dwa pełne przebiegi kalibratora zużyły 101 i 104 wywołania, a licznik
`x-ratelimit-used` po całej sesji testowej pokazał 255. Zgadza się.

Przy 5 000/h core → **~50 pełnych przebiegów na godzinę**. Limit nie jest wąskim
gardłem; wąskim gardłem jest latencja (§2.2). Przy TTL i seed dumpie realny koszt
kolejnych przebiegów spada do kilku wywołań.

### 4.4 Pułapki: przeczytać przed pisaniem kodu

1. **Pole `assignee` (liczba pojedyncza) zostało usunięte** w wersji API
   `2026-03-10`, razem z `has_downloads` i `merge_commit_sha`. Czytać wyłącznie
   tablicę `assignees`. `has_assignee = assignees.Count > 0`.
2. **Advanced search jest domyślny od 4 września 2025.** Spacja w `q` to teraz
   operator **AND** (wcześniej OR). Parametr `advanced_search=true` nadal jest
   akceptowany, ale jest zbędny, nie dodawać dla czystości, nie dlatego, że
   błędny.
3. **`size` to rozmiar repozytorium git w KB**, nie LOC. Zawiera grafikę, testy,
   assety. Używamy jako proxy złożoności i **tak to opisujemy w UI i README**.
4. **`no:assignee` ≠ „nikt tego nie robi".** Wiele issues jest zaklepanych w
   komentarzu bez przypisania. Wykrycie wymagałoby pobierania komentarzy per
   issue, poza budżetem. Ograniczenie idzie do README.
5. **`/issues` zwraca też pull requesty.** Elementy z polem `pull_request` trzeba
   odfiltrować, inaczej issue turnaround jest zafałszowany.
6. **`open_issues_count` w obiekcie repo liczy issues razem z PR-ami.** Nie
   używać jako liczby issues.
7. **`/stats/commit_activity` zwraca 202 z pustym body**, gdy GitHub dopiero
   liczy statystyki. **Nie używamy go w ogóle**, bo velocity liczymy z `pushed_at`.
8. **Wyniki search są ucięte na 1000**, max 100 na stronę. Nie budować paginacji
   „do końca".
9. **Odpowiedź 304 z ETagiem nie zmniejsza limitu, ale tylko tam, gdzie
   powtarzamy to samo wywołanie.** Metadane repozytoriów przychodzą teraz z
   `/search/repositories`, więc `/repos/{o}/{n}` w ogóle nie wołamy i nie ma tam
   czego cache'ować warunkowo. ETagi zakładamy na **wywołania per-finalista**:
   `/issues` i `/pulls`, bo to one powtarzają się między przebiegami i to one
   zjadają 95 % puli core.
10. **Stale ratio jest próbką, nie pomiarem.** `per_page=100&direction=asc` daje
    100 **najstarszych** otwartych PR-ów. Jeśli wróciło dokładnie 100, wiemy
    tylko, że otwartych jest ≥100, a próbka jest z definicji najgorsza. Wtedy
    komponent oznaczamy jako próbkowany i opisujemy w UI: „spośród 100
    najstarszych otwartych PR-ów". Jeśli wróciło <100, ratio jest dokładne.
    **Zmierzone: w paśmie `100..2500` próbkowanie nie wystąpiło ani razu** (0 z 25
    i 0 z 24 finalistów miało 100 otwartych PR-ów). Problem jest realny dopiero
    przy wysokich pasmach gwiazdek, kod obsługuje go, ale w domyślnym scenariuszu
    się nie odpala.
11. **Sortowanie kształtuje wyniki mocniej niż filtry, sprawdzone dwa razy.**
    `sort=updated` w wyszukiwaniu issues premiuje repo z generowanym backlogiem
    (§0.1). `sort=stars` w wyszukiwaniu repozytoriów bierze wyłącznie czubek
    rozkładu i zeruje Community Fit oraz Complexity Match (§0.4). **W repo search
    nie podajemy `sort` w ogóle**: zakres wielkości kontrolujemy pasmem
    `stars:{lo}..{hi}` w zapytaniu, a kolejność ustala nasz własny Match Score.
12. **Nagłówki `X-RateLimit-*` z realnych odpowiedzi, NIE endpoint `/rate_limit`.**
    Zmierzone: po 255 wykorzystanych wywołaniach `GET /rate_limit` nadal
    raportował `core: 5000/5000`, podczas gdy nagłówek `x-ratelimit-used` na
    zwykłym żądaniu pokazywał poprawne 255. Logujemy `X-RateLimit-Remaining` /
    `-Reset` / `-Used` przy każdej odpowiedzi i to je wystawiamy w `/api/health`.
13. **Limit search 30/min jest realny i łatwo w niego wejść.** Podczas przemiatania
    dziewięciu języków po cztery pasma (36 zapytań) GitHub zaczął odrzucać wyniki
    po przekroczeniu trzydziestki. Przy 3-4 językach na przebieg to nieproblem,
    ale przy równoległych użytkownikach trzeba kolejkować wyszukiwania.

### 4.5 Klient

`Octokit.NET` (14.x) do zapytań, które pokrywa. Dla repo search i GraphQL,
własny `HttpClient` przez `IHttpClientFactory` z polityką Polly (retry z
backoffem na 403/429 i 5xx, honorowanie `Retry-After`). Interfejsy
`IGitHubClient` i `ILanguageSource` w `Core/Abstractions`, implementacje w
`Infrastructure`, dzięki temu testy nie dotykają sieci.

---

## 5. Pipeline rekomendacji

```
1. PROFIL         a) GET /users/{login}/repos              (cache 24 h)
                     → odfiltruj forki
                     → języki własne: ranking po liczbie repo
                     → median_size_kb = mediana size z nie-forków
                     → interests = suma topics (lowercase)

                  b) POST /graphql  repositoriesContributedTo  (cache 24 h)
                     → języki z kontrybucji + rozkład bajtów
                     → OPCJONALNE: przy braku czasu pomijamy, profil
                       leci z samych własnych repo (patrz §12)

                  c) połącz oba źródła → lista języków z etykietą źródła
                     → zwróć do UI jako chipy, wstępnie zaznaczone (max 3)

2. KANDYDACI      pasmo gwiazdek z preferencji użytkownika (log-symetryczne):
                    lo = max(100, targetStars / 5)
                    hi = targetStars · 5
                    domyślne targetStars=500  →  stars:100..2500

                  dla każdego ZAZNACZONEGO języka (max 3-4):
                  GET /search/repositories
                    q = language:{lang} good-first-issues:>=2
                        stars:{lo}..{hi} pushed:>{dziś − 90 dni}
                        archived:false fork:false
                    per_page=50
                    BEZ parametru sort  ← celowo, patrz §0.4
                  → deduplikuj po full_name
                  → ~100 kandydatów Z KOMPLETNYMI METADANYMI, 0 dodatkowych wywołań
```

> **Dlaczego ten kierunek.** Pierwotny plan szedł od wyszukiwania issues i
> wyprowadzał repozytoria z wyników. Dane (§0.1) pokazały, że taki zestaw jest
> zdominowany przez generowany spam, a filtry jakości (`stars`, `pushed_at`)
> wymagałyby wywołania `/repos/{o}/{n}` dla każdego kandydata, czyli 30 zapytań,
> zanim w ogóle wiemy, kogo odrzucić. Kwalifikator `good-first-issues:>=n` w
> wyszukiwaniu repozytoriów rozwiązuje jedno i drugie: bramka jakości działa po
> stronie GitHuba, a odpowiedź zawiera pełne metadane.

```
3. TWARDE FILTRY  większość załatwia już zapytanie. Lokalnie odrzucamy jeszcze:
                    disabled | has_issues == false
                    brak licencji OSS (opcjonalnie, jako sygnał)

4. MATCH SCORE    policz dla WSZYSTKICH ~100 kandydatów, 0 wywołań,
                  bo metadane przyszły z search
                  → posortuj, weź TOP 25

                  25, nie 20: krok 5 odsiewa repo bez wolnych issues, więc
                  lejek się zwęża. Przy 20 finalistach i typowym odsiewie
                  zostaje ~13, poniżej dziesiątki wymaganej na wyjściu
                  brakuje wtedy zapasu.

5. ISSUES         dla 25 finalistów, 1 wywołanie na repo (cache 1 h)
                  → filtruj: assignees.Count == 0
                  → odfiltruj elementy z polem pull_request
                  → policz difficulty (§6.3)
                  → repo bez ani jednego wolnego issue wypada z listy

6. HEALTH SCORE   dla pozostałych finalistów, 3 wywołania na repo (cache 12 h)

7. RANKING FINAŁ  final = 0.65 · match + 0.35 · health
                  → zwróć TOP 10 z pełnym rozbiciem obu score'ów
```

**Świadomy kompromis do opisania w README:** Match liczymy dla wszystkich ~100
kandydatów, ale Health tylko dla 25 najlepiej dopasowanych. Bardzo zdrowe repo z
niskim Match nadal nie wypłynie. Kompromis jest jednak mniejszy niż w rewizji 1
(25 zamiast 15 finalistów, a Match liczony na komplecie metadanych zamiast na
30 wybranych na ślepo).

---

## 6. Scoring

Wszystko w `Gittez.Core/Scoring` jako **czyste funkcje**. Każdy komponent
zwraca strukturę:

```csharp
public sealed record ScoreComponent(
    string Key,           // "language_match"
    string Label,         // "Dopasowanie języka"
    double? Points,       // 24.0, null gdy za mało danych
    double MaxPoints,     // 30.0
    string RawValue,      // "C#"
    string Explanation,   // "C# jest Twoim 2. językiem (7 repo, 2 kontrybucje)"
    bool IsSampled);      // true = liczone na niepełnej próbce, patrz §4.4 pkt 10
```

`Explanation` **nie jest generowane w UI**, powstaje razem z liczbą, w tym samym
miejscu. To gwarantuje, że opis nigdy się nie rozjedzie z wynikiem.

### 6.1 Match Score (0-100)

Wagi zmienione względem rewizji 1, uzasadnienie w §0.2.

| Komponent | Waga r1 | Waga r2 | Powód zmiany |
|-----------|---------|---------|--------------|
| Language Match | 30 | **30** | - |
| Topic Match | 20 | **25** | najlepiej różnicujący komponent w praktyce |
| Complexity Match | 20 | **25** | przejmuje 5 pkt po wyciętym Freshness |
| Community Fit | 15 | **20** | przejmuje 5 pkt po Freshness |
| Issue Freshness | 15 | **-** | **wycięty**, patrz §0.2 |

Cztery komponenty, suma 100.

**Language Match (30 pkt)**
```
30  repo.language == user.top[0]
24  repo.language ∈ user.top[1..2]
15  repo.language ∈ user.top[3..4]
 6  repo.language ∈ pozostałe języki użytkownika
 0  brak
```
Ranking języków użytkownika łączy dwa źródła: własne repozytoria i repozytoria,
do których kontrybutował. Kontrybucja waży tyle co własne repo. Świadomie, bo
merged PR do cudzego projektu mówi o umiejętnościach nie mniej niż własne repo.
Wyjaśnienie w UI rozbija to na oba źródła.

**Complexity Match (25 pkt): percentyl w puli kandydatów**
```
pool   = rozmiary (KB) wszystkich kandydatów tego przebiegu
mniejsze_od = |{ s ∈ pool : s > repo.size_kb }|
points = 25 · mniejsze_od / (|pool| − 1)
```
Opis w UI: „mniejsze niż 78 % kandydatów, mniej kodu do ogarnięcia na start".

**Dlaczego nie stosunek do mediany użytkownika.** Pierwotny wzór
`ratio = repo.size / max(user.median, 100)` z progami 0,3-3 i 0,1-10 został
zmierzony na żywych danych (§0.5) i **dawał minimum ponad połowie kandydatów**:
mediana rozmiaru repo w puli to ~44 MB, mediana własnych projektów studenta ~337 KB.
Porównanie projektu hobbystycznego z produkcyjną bazą kodu zawsze zwraca „dużo
większy", więc komponent nie niósł informacji.

Percentyl z definicji rozkłada się na całym zakresie i zmienia pytanie na takie,
na które da się odpowiedzieć: **nie „czy to repo jest jak Twoje", tylko „czy jest
przystępne na tle innych kandydatów"**. Dla wchodzącego w OSS to użyteczniejsze.

Uwaga implementacyjna: funkcja przyjmuje `(repo, pool)` zamiast `(repo, user)`,
nadal czysta, nadal bez I/O, ale test musi podać całą pulę.
Dzielnik jest klamrowany do minimum 100, więc dzielenie przez zero jest
niemożliwe nawet dla pustego profilu. Test to weryfikuje.

**Community Fit (20 pkt)** (gaussian w skali logarytmicznej)
```
target = preferencja użytkownika (domyślnie 500 gwiazdek)
d      = log10(stars + 1) − log10(target + 1)
points = 20 · exp(−d² / 0.5)
```
Maksimum w preferowanym rzędzie wielkości, płynny spadek w obie strony. Za małe
repo = brak mentoringu, za duże = giniesz w tłumie.

**Ten komponent działa wyłącznie wtedy, gdy pula kandydatów ma rozstrzał**, a
ma go tylko dlatego, że `targetStars` wchodzi do zapytania jako pasmo
`stars:{lo}..{hi}` (§5 krok 2). Przy `sort=stars` i otwartym progu `stars:>=100`
cała pula ląduje w przedziale 5k-55k gwiazdek i komponent zwraca 0-2 pkt dla
każdego repozytorium, 20 punktów bez żadnej informacji (§0.4). Test „100× target
→ wartość bliska 0" pilnuje wzoru, ale to zapytanie decyduje, czy komponent ma
sens.

**Topic Match (25 pkt)**
```
repo.topics puste  LUB  user.interests puste  →  komponent = null

overlap = |repo.topics ∩ user.interests|
points  = 25 · min(overlap, 3) / 3
```
**Puste `topics` to `null`, nie zero.** Brak topików jest zaniedbaniem
maintainera w metadanych, a nie informacją o braku dopasowania, przy wadze 25
karalibyśmy dobre repozytorium ćwiartką wyniku za nieuzupełnione pole. W próbce
z §0.4 dotyczyło to `dotnet/eShop`. Ta sama zasada co przy Merge Rate: wynik
procentuje się po dostępnych komponentach (§6.2).

`user.interests` zawiera wyłącznie `topics` z repozytoriów użytkownika, **bez
nazw języków**, inaczej repo z topikiem `csharp` dostawałoby punkty dwa razy:
za język i za temat.

**Brak komponentu „świeżość issues", świadomie.** Wyleciał w całości (§0.2).
Gwarancję, że jest w co kliknąć, daje filtr w kroku 5 pipeline'u, a nie punkty.
W UI komunikujemy to raz, nad listą: „wszystkie wyniki mają co najmniej jedno
nieprzypisane issue".

### 6.2 Health Score (0-100)

Liczony z ostatnich 30 PR-ów, **z pominięciem draftów i botów**
(`user.type == "Bot"`, loginy kończące się na `[bot]`). Trzy wywołania na
repozytorium, Commit Velocity przychodzi za darmo z repo search.

**Merge Rate (25 pkt)**
```
resolved = PR-y z closed_at != null
rate     = count(merged_at != null) / count(resolved)
points   = 25 · rate
```
Jeśli `resolved < 5` → komponent = `null`, w UI „za mało danych", a maksimum
Health Score obniżamy o 25 (procentowanie po dostępnych komponentach).

**Resolution Latency (25 pkt)**: mediana godzin od `created_at` do
`merged_at ?? closed_at`
```
25  ≤ 2 h
19  ≤ 12 h
13  ≤ 48 h
 7  ≤ 7 dni
 2  powyżej
```
**Progi skalibrowane na zmierzonym rozkładzie** (§0.5), nie na przeczuciu.
Zmierzone mediany w puli finalistów: p25 = 1 h, p50 = 2-3 h, p75 = 17-24 h,
max = 199 h. Pierwotny próg „≤ 48 h → 25 pkt" dawał maksimum niemal wszystkim,
bo pula jest już przefiltrowana przez `pushed:>90d` i `good-first-issues`,
trafiają do niej wyłącznie projekty, które szybko obracają PR-ami.
> To **nie** jest czas do pierwszego review. Ten wymagałby wywołania
> `/pulls/{n}/reviews` osobno dla każdego PR, 30 zapytań na repo zamiast
> jednego. Świadome przybliżenie, opisane w README.

**Stale Ratio (20 pkt)**
```
open   = pobrane otwarte PR-y (max 100, najstarsze najpierw)
stale  = te z created_at starszym niż 90 dni
points = 20 · (1 − stale / count(open))

count(open) == 0   → komponent = null, nie 20 pkt
count(open) == 100 → IsSampled = true, opis mówi o próbce
```
Zero otwartych PR-ów daje `null`, nie maksimum. Brak PR-ów nie jest dowodem
zdrowia, częściej jest dowodem, że nikt nic nie zgłasza.

**Commit Velocity (15 pkt)**: z `pushed_at`, 0 wywołań
```
15  ≤ 7 dni
11  ≤ 30 dni
 5  ≤ 90 dni
 0  powyżej
```

**Issue Turnaround (15 pkt)**: mediana dni do zamknięcia z 30 ostatnich
zamkniętych issues (**po odfiltrowaniu elementów z polem `pull_request`**)
```
15  ≤ 7 dni
11  ≤ 30 dni
 6  ≤ 90 dni
 2  powyżej
```

**Procentowanie przy brakujących komponentach (wspólne dla obu score'ów):**
```
score = 100 · Σ(dostępne punkty) / Σ(maxPoints dostępnych komponentów)
```
Ta sama funkcja obsługuje Match (gdzie `null` może zwrócić Topic Match) i Health.
Jeśli wszystkie komponenty Health są `null` → Health = `null`, a `finalScore` =
sam Match, z adnotacją w UI.

### 6.3 Heurystyka trudności issue

Bez zgadywania LOC, wyłącznie z pól, które lista issues już zwróciła:
```
1 (łatwe)    label zawiera docs|documentation|typo|readme|translation
             LUB (body_length < 500 AND comment_count ≤ 2)
3 (trudne)   comment_count > 10 LUB body_length > 3000
2 (średnie)  pozostałe
```
W UI opisane jako „szacunek heurystyczny", nie jako fakt.

---

## 7. Kontrakty API

Base: `/api`. Sesja: nagłówek `X-Session-Id: <uuid>`, generowany w przeglądarce
przy pierwszej wizycie i trzymany w `localStorage`. **To nie jest uwierzytelnienie**,
identyfikator da się podrobić, ale nie chroni niczego wrażliwego. Napisać
wprost w README.

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/api/profile/{login}` | profil + wykryte języki ze źródłem |
| `GET` | `/api/recommendations` | `?login=&languages=C%23,TypeScript&targetStars=&maxDifficulty=&limit=` |
| `GET` | `/api/repos/{owner}/{name}` | szczegóły + pełne rozbicie + issues |
| `GET` | `/api/watchlist` | pozycje bieżącej sesji |
| `POST` | `/api/watchlist` | `{ repoFullName, note? }` → 201 |
| `PATCH` | `/api/watchlist/{id}` | `{ note }` → **ścieżka edycji** |
| `DELETE` | `/api/watchlist/{id}` | 204 |
| `GET` | `/api/health` | status bazy + `rateLimitRemaining` + `rateLimitReset` |

### 7.1 Kształt profilu

```jsonc
{
  "login": "octocat",
  "publicRepoCount": 14,
  "medianSizeKb": 2400,
  "languages": [
    { "name": "C#",         "ownedRepos": 7, "contributedRepos": 2, "rank": 1 },
    { "name": "TypeScript", "ownedRepos": 3, "contributedRepos": 1, "rank": 2 },
    { "name": "Python",     "ownedRepos": 1, "contributedRepos": 0, "rank": 3 }
  ],
  "interests": ["blazor", "esp32", "embedded"],
  "computedAt": "2026-07-28T09:00:00Z"
}
```

### 7.2 Kształt rekomendacji

```jsonc
{
  "fullName": "MudBlazor/MudBlazor",
  "description": "…",
  "htmlUrl": "https://github.com/MudBlazor/MudBlazor",
  "stars": 8900,
  "primaryLanguage": "C#",
  "topics": ["blazor", "material-design"],
  "lastPushedAt": "2026-07-27T10:12:00Z",
  "matchScore": 78.5,
  "healthScore": 84.0,
  "finalScore": 80.4,
  "matchBreakdown": [ /* ScoreComponent[] */ ],
  "healthBreakdown": [ /* ScoreComponent[], Points może być null */ ],
  "issues": [
    {
      "number": 1234,
      "title": "…",
      "htmlUrl": "…",
      "labels": ["good first issue", "area-docs"],
      "commentCount": 1,
      "difficulty": 1,
      "updatedAt": "2026-07-26T…"
    }
  ],
  "dataFreshness": { "repo": "2026-07-28T09:00:00Z", "health": "2026-07-28T09:00:00Z" }
}
```

### 7.3 Obsługa błędów

Jednolity `ProblemDetails` (RFC 7807). Kody:

| Sytuacja | Status | `type` |
|----------|--------|--------|
| Login nie istnieje | 404 | `github-user-not-found` |
| Użytkownik bez publicznych repo | 422 | `insufficient-profile-data` |
| Limit GitHuba wyczerpany **i cache pusty** | 503 + `Retry-After` | `github-rate-limited` |
| Limit wyczerpany, **cache ma dane** | 200 + `X-Data-Stale: true` | - |
| Brak wyników po filtrach | 200 z pustą tablicą + `hints[]` | - |
| Brak lub niepoprawny `X-Session-Id` | 400 | `missing-session` |
| `repoFullName` spoza formatu `owner/name` | 400 | `invalid-repo-name` |
| Notatka dłuższa niż 500 znaków | 400 | `note-too-long` |
| Repo już na watchliście sesji | 409 | `already-on-watchlist` |
| Sesja ma już 100 pozycji na watchliście | 409 | `watchlist-full` |
| Pozycja nie istnieje **lub należy do innej sesji** | 404 | `watchlist-item-not-found` |

**Cudza pozycja zwraca 404, nie 403** - potwierdzenie, że coś istnieje pod danym
id, byłoby wyciekiem informacji przy identyfikatorze, który da się podrobić.

**409 rozstrzyga też wyścig.** Sprawdzenie duplikatu przed zapisem przegrywa z
równoległym żądaniem, więc `DbUpdateException` z unikalnego indeksu
`(session_id, repo_full_name)` też mapuje się na `already-on-watchlist` - inaczej
podwójne kliknięcie w gwiazdkę kończy się piątką.

**503 tylko wtedy, gdy nie mamy czym poratować.** Dopóki cache cokolwiek zawiera,
serwujemy stare dane z nagłówkiem `X-Data-Stale`, a front pokazuje banner. Puste
demo z komunikatem o błędzie jest gorsze niż lekko nieświeże dane.

**Brak wyników zwracamy jako 200, nie 404**, bo to poprawny wynik zapytania, a nie
błąd. `hints` podpowiada, co poluzować („zmniejsz próg gwiazdek", „dodaj język").

---

## 8. Cache

| Zasób | TTL | Klucz | ETag |
|-------|-----|-------|------|
| profil | 24 h | `github_login` | - |
| metadane repo | 6 h | `full_name` | - (przychodzą z search) |
| issues repo | 1 h | `repo_full_name` | **tak**, `If-None-Match` |
| health score | 12 h | `full_name` | **tak**, na oba wywołania `/pulls` |

Kolumna `etag` w `repo_cache` trzyma ETag **ostatniego wywołania per-finalista**,
nie metadanych repo. Te przychodzą z `/search/repositories` i nie są odpytywane
warunkowo (§4.4 pkt 9).

### 8.1 Seed: ładowany po migracjach, nie przez initdb

**Nie montować `db/seed/` w `/docker-entrypoint-initdb.d/`.** Skrypty initdb
odpalają się przy inicjalizacji pustego katalogu danych, czyli **zanim** API
zdąży zaaplikować migracje EF. `INSERT INTO repo_cache` trafi w nieistniejącą
tabelę, a entrypoint Postgresa przerwie start kontenera. Gdyby seed sam tworzył
tabelę, wywali się migracja.

Poprawnie: `DatabaseSeeder` w `Infrastructure/Persistence`, wołany w
`Program.cs` **po** `db.Database.MigrateAsync()`, za flagą `SEED_ON_STARTUP=true`.
Skrypt zawiera `INSERT ... ON CONFLICT (full_name) DO NOTHING`, więc jest
idempotentny i restart kontenera niczego nie psuje.

Zawartość: ~40 repozytoriów (.NET, React, TypeScript, Python, Go, Rust, Swift) z
policzonym Health Score. Efekt: demo działa natychmiast po starcie i **nie umiera,
gdy limit GitHuba się wyczerpie w trakcie oceniania**.

---

## 9. Frontend

Vite + React 19 + TypeScript + TanStack Query + Tailwind. Bez shadcn, jeśli
zabraknie czasu: kilka własnych komponentów wystarczy.

```
web/src/
├── api/client.ts          # typowany fetch, wstrzykuje X-Session-Id
├── api/types.ts           # DTO 1:1 z backendem
├── hooks/
│   ├── useSession.ts      # UUID w localStorage
│   ├── useProfile.ts      # wykryte języki
│   ├── useRecommendations.ts
│   └── useWatchlist.ts    # mutacje z optimistic update
├── components/
│   ├── LanguageChips.tsx  # wykryte języki, odznaczalne, ze źródłem
│   ├── RepoCard.tsx
│   ├── ScoreRing.tsx      # kołowy wskaźnik Match
│   ├── HealthBadge.tsx
│   ├── ScoreBreakdown.tsx # modal z dwiema listami komponentów
│   ├── IssueChip.tsx
│   └── states/            # Skeleton, ErrorState, EmptyState, StaleBanner
├── pages/
│   ├── Landing.tsx
│   ├── Results.tsx
│   └── Watchlist.tsx
└── App.tsx
```

**Ekrany**

1. **Landing**: input na login GitHub. Po jego wypełnieniu strzał do
   `/api/profile/{login}` i pokazanie **wykrytych języków jako chipów**,
   wstępnie zaznaczonych, z etykietą źródła: „C#, 7 repo, 2 kontrybucje".
   Użytkownik odznacza, dokłada, klika Szukaj. Do tego suwak „preferowana
   wielkość projektu" (`targetStars`) i przełącznik maksymalnej trudności.

   **Suwak jest sterowaniem wyszukiwaniem, nie tylko wagą.** `targetStars`
   wyznacza pasmo `stars:{lo}..{hi}` w zapytaniu (§5), więc przesunięcie go
   zwraca **inne repozytoria**, a nie te same karty z przeliczonymi punktami.
   Pokaż aktualne pasmo pod suwakiem („szukam w przedziale 100-2 500 ⭐"),
   recenzent od razu widzi, że kontrolka coś robi.

   To jest ten ekran preferencji, który był na liście cięć. Wraca, bo kosztuje
   jeden komponent, a **czyni demo interaktywnym**: recenzent widzi, że narzędzie
   poprawnie odczytało jego stack, przestawia jeden chip i dostaje inne wyniki.

2. **Results**: nad listą jedno zdanie o gwarancji filtra: „wszystkie wyniki
   mają co najmniej jedno nieprzypisane issue". To zastępuje wycięty komponent
   punktowy (§0.2). Poniżej siatka kart. Karta: nazwa, gwiazdki, opis, pierścień Match,
   plakietka Health, 1-3 chipy z issues, „Dlaczego?" → modal, gwiazdka → watchlist.
   Karty z niskim Health dostają ostrzegawczą plakietkę zamiast być ukrywane,
   kontrast jest dowodem, że ocena cokolwiek mierzy.

   **`finalScore` nie pojawia się na karcie jako liczba.** Służy wyłącznie do
   ustalenia kolejności. Powód jest zmierzony (§0.5): w widocznej dziesiątce
   wyniki końcowe mieszczą się w 6,8 punktu, więc wielka liczba sugerowałaby
   precyzję, której nie ma. Poszczególne komponenty różnicują świetnie
   (Topic 0-25, Complexity 0-25, Merge 0-25, Stale 0-20).

   Zamiast tego karta niesie **jedno zdanie z komponentu, który najmocniej
   wyróżnia to repo na tle pozostałych**, „mniejsze niż 78 % kandydatów",
   „96 % PR-ów zmergowanych", „3 wspólne tematy". Zdanie bierzemy z gotowego
   `ScoreComponent.Explanation`, więc nie powstaje żadna nowa logika opisowa.

3. **ScoreBreakdown (modal)**: dwie listy pasków: komponent, punkty/max,
   wartość, zdanie wyjaśnienia. Komponenty z `Points == null` renderują się jako
   „za mało danych" na szaro; z `IsSampled == true` dostają dopisek o próbce.
   To jest serce projektu, ma wyglądać najlepiej. Zwykłe divy z Tailwindem są
   szybsze i czytelniejsze niż Recharts.

4. **Watchlist**: zapisane repo, inline edycja notatki (ścieżka edycji),
   usuwanie.

**Stany obowiązkowe** (wymóg wprost w dokumencie CetusPro): skeleton podczas
ładowania, `ErrorState` z przyciskiem ponowienia, `EmptyState` z podpowiedziami
z `hints[]`, banner o nieświeżych danych.

---

## 10. Testy

**Jednostkowe (`Gittez.Tests/Scoring`)** (priorytet, nie odpuszczać):
- Language Match: trafienie w top1 / top3 / brak / repo bez języka (`null`).
- Language Match: język wyłącznie z kontrybucji, bez własnego repo → liczy się.
- Complexity Match (percentyl): najmniejsze repo w puli → 25 pkt, największe → 0,
  pula jednoelementowa → brak dzielenia przez zero.
- Complexity Match: dwa repo o identycznym rozmiarze dostają identyczny wynik.
- Community Fit: dokładnie w targecie → 20 pkt; 100× target → wartość bliska 0.
- **Pasmo gwiazdek: `targetStars=500` → `stars:100..2500`; `targetStars=50` →
  `lo` klamrowane do 100, nie 10.** Test regresji na błąd z §0.4, bez tego
  pasma Community Fit jest stale zerowy.
- Topic Match: nazwa języka w `topics` repo **nie** daje punktów tematycznych.
- **Topic Match: puste `repo.topics` → `null`, nie 0**, a Match procentuje się po
  pozostałych trzech komponentach (maks. 75). Test regresji, bez tego repo bez
  uzupełnionych metadanych traci ćwiartkę wyniku bez powodu.
- Match Score: wszystkie cztery komponenty dostępne → maks. dokładnie 100.
- Merge Rate: mniej niż 5 rozstrzygniętych PR-ów → `null`, nie 0.
- Stale Ratio: zero otwartych PR-ów → `null`, nie 20.
- Stale Ratio: dokładnie 100 zwróconych PR-ów → `IsSampled == true`.
- Issue Turnaround: wejście zawierające PR-y → muszą zostać odfiltrowane.
- Difficulty: label `docs` → 1; 15 komentarzy → 3.
- Health Score z brakującym komponentem: procentowanie po dostępnych.
- Health Score ze wszystkimi komponentami `null` → `null`, `finalScore` = Match.

Testy tabelaryczne (`[Theory]` + `[InlineData]`). Cel: ~22 przypadki, wszystkie
bez I/O.

> Stan po implementacji: 51 metod testowych w 7 plikach, wszystkie bez sieci.
> Doszły testy pipeline'u na degradację, których ten spis nie przewidywał.

**Integracyjny, jeden, dla ścieżki zapisu:** `WebApplicationFactory` +
`IGitHubClient` podmieniony na fake → `POST /api/watchlist` → `PATCH` → `GET`
zwraca zaktualizowaną notatkę. Dowodzi, że przepływ zapisu działa end-to-end,
łącznie z leniwym tworzeniem sesji.

Testcontainers pomijamy, bo narzut czasowy większy niż wartość przy tym deadline.

---

## 11. Uruchomienie i deployment

**`docker compose up` musi wystarczyć.** Trzy usługi: `db`, `api`, `web`.
- `db`: postgres:16, wolumen. **Bez montowania seeda w initdb** (§8.1).
- `api`: multi-stage build na obrazie .NET 10, healthcheck na `/api/health`,
  migracje przy starcie gdy `APPLY_MIGRATIONS=true`, seed gdy `SEED_ON_STARTUP=true`.
- `web`: build Vite → nginx, proxy `/api` na kontener `api`.
- `depends_on` z `condition: service_healthy`.

**`.env.example`**: `GITHUB_TOKEN=`, `POSTGRES_PASSWORD=`, `API_BASE_URL=`.
Realny token **nigdy** w repo ani w komendach wklejanych gdziekolwiek; sprawdzić
historię gita przed wysłaniem.

**Wersja online.** Plan brzmiał: backend na Railway (Postgres w cenie), front na
Vercel z `VITE_API_BASE_URL` na URL Railway, CORS na backendzie ograniczony do
domeny frontu. Wyszło inaczej i lepiej: oba obrazy stoją na Railwayu, `api` bez
domeny publicznej, `web` z domeną publiczną i nginxem proxującym `/api` po sieci
prywatnej do `api.railway.internal`. Front i API zostają na jednym originie, więc
`VITE_API_BASE_URL` zostaje puste, a konfiguracja CORS jest niepotrzebna zamiast
być napisana. Obrazy budują się w Actions i lądują w GHCR, Railway ciąga gotowe.

Deploy robimy **w środku dnia, nie w nocy przed deadlinem**, bo to najczęstsze
miejsce, gdzie projekty się wykrwawiają. Kliknięcia w panelu, wartości zmiennych
i pułapka z portem docelowym domeny są opisane osobno w `docs/DEPLOY.md`.

---

## 12. Harmonogram

### Dziś (28 lipca) wieczorem: fundament

| Blok | Zadanie | Gotowe gdy |
|------|---------|-----------|
| 1 | Solucja .NET 10, Compose z Postgresem, migracja `Initial` | `docker compose up` wstaje, tabele istnieją |
| 2 | `IGitHubClient`: profil z własnych repo, repo search, issues finalisty | ręczny strzał zwraca realne dane |
| 3 | `ScoringService`, Match Score, 5 komponentów + testy | testy zielone |
| 4 | `GET /api/recommendations` zwraca realne 10 pozycji, Scalar działa | odpowiedź w przeglądarce |

Cel na koniec dnia: **backend zwraca sensowne rekomendacje**. Front może nie
istnieć.

### Jutro (29 lipca)

| Blok | Zadanie |
|------|---------|
| Rano | Health Score (3 wywołania/repo) + cache + seed po migracjach + degradacja |
| Rano | Vite, `LanguageChips`, `RepoCard`, lista wyników na realnych danych |
| Południe | `ScoreBreakdown` modal, wszystkie stany UI, watchlist z edycją notatki |
| **14:00** | **Deploy Railway + Vercel, weryfikacja live** ← nie przesuwać (wyszło: oba obrazy na Railwayu, §11) |
| Po deployu | GraphQL `repositoriesContributedTo`, jeśli reszta stoi |
| Po deployu | README, zrzuty ekranu, krótkie nagranie przepływu (nagranie ucięte, patrz kolejność cięcia niżej) |
| **~18:00** | Formularz zgłoszeniowy |
| 23:50 | Lekkie opóźnienia |

### Kolejność cięcia, gdy czas się kończy

Ucinamy od dołu, nigdy od góry:
1. ~~Watchlist~~ ← **nie**, to jest wymóg ścieżki full stack
2. ~~Chipy języków~~ ← **nie**, to jest interaktywność demo za jeden komponent
3. Nagranie wideo (zrzuty ekranu i demo wystarczą)
4. Test integracyjny (unit testy zostają)
5. Strona szczegółów jako osobny route (modal wystarczy)
6. **GraphQL `repositoriesContributedTo`**, profil leci z samych własnych
   repozytoriów, a gotowe zapytanie ląduje w README w „Czego nie zdążyłem".
   Konkret z wklejonym zapytaniem wygląda lepiej niż ogólnik.

---

## 13. Mapowanie na kryteria oceny CetusPro

| Kryterium | Gdzie to widać |
|-----------|----------------|
| Wartość | realny problem, jasny użytkownik, autentyczna motywacja (własny PR do OSS) |
| MVP | login → języki → rekomendacje → rozbicie → zapis na watchlistę, kompletny end-to-end |
| Jakość | `Core` bez zależności, czyste funkcje scoringu, 51 testów, migracje, Scalar |
| Architektura | proporcjonalna: brak Redisa, brak mikroserwisów, cache w bazie, którą i tak mam; współbieżność ograniczona semaforem z uzasadnieniem |
| Testowanie | przypadki brzegowe + **test regresji na błąd wykryty w danych** (§0.2) |
| Uruchamialność | `docker compose up`, healthcheck, seed po migracjach, demo działa bez tokenu |
| Komunikacja | README z sekcją decyzji, świadomych ograniczeń i **zmian wymuszonych przez pomiar** |
| Prezentacja | live demo pod adresem z README |
