#!/usr/bin/env python3
"""
Kalibrator scoringu GitBounty.

Liczy pełny pipeline na żywym API i pokazuje rozstrzał każdego komponentu.
Odpowiada na pytanie, czy scoring cokolwiek różnicuje: komponent, który wszystkim
daje podobnie, jest w interfejsie opartym na wytłumaczalności bezwartościowy.

    ./calibrate.py
    ./calibrate.py --langs Swift,C#
    ./calibrate.py --target 300 --workers 12
"""
import argparse, json, math, os, statistics as st, sys, time, urllib.error, urllib.parse, urllib.request
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone

API = "https://api.github.com"
NOW = datetime.now(timezone.utc)


def load_env(path=None):
    # .env leży w katalogu głównym repo, niezależnie skąd odpalono skrypt
    path = path or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".env")
    if os.path.exists(path):
        for line in open(path):
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                os.environ.setdefault(k.strip(), v.strip())


load_env()
TOKEN = os.environ.get("GITHUB_TOKEN", "")
if not TOKEN:
    sys.exit("Brak GITHUB_TOKEN w .env")

_calls = {"n": 0, "ms": 0.0}


def gh(path, graphql=None):
    url = f"{API}/graphql" if graphql else API + path
    data = json.dumps(graphql).encode() if graphql else None
    req = urllib.request.Request(url, data=data, method="POST" if graphql else "GET")
    req.add_header("Authorization", f"Bearer {TOKEN}")
    req.add_header("Accept", "application/vnd.github+json")
    if graphql:
        req.add_header("Content-Type", "application/json")
    t0 = time.time()
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            body = json.loads(r.read())
    except urllib.error.HTTPError as e:
        body = {"__error": e.code, "__body": e.read()[:200].decode("utf8", "ignore")}
    except Exception as e:
        body = {"__error": str(e)}
    _calls["n"] += 1
    _calls["ms"] += (time.time() - t0) * 1000
    return body


def parse_dt(s):
    return datetime.fromisoformat(s.replace("Z", "+00:00")) if s else None


def profile(login):
    repos = gh(f"/users/{login}/repos?per_page=100&sort=pushed")
    if not isinstance(repos, list):
        return {"median_kb": 500, "interests": set(), "n": 0}
    own = [r for r in repos if not r["fork"]]
    sizes = sorted(r["size"] for r in own) or [500]
    interests = set()
    for r in own:
        interests |= {t.lower() for t in (r.get("topics") or [])}
    return {"median_kb": st.median(sizes), "interests": interests, "n": len(own)}


def candidates(lang, lo, hi, pushed):
    q = (f'language:{lang} good-first-issues:>=2 stars:{lo}..{hi} '
         f'pushed:>{pushed} archived:false fork:false')
    res = gh("/search/repositories?q=" + urllib.parse.quote(q) + "&per_page=100")
    items = res.get("items", []) if isinstance(res, dict) else []
    for it in items:
        it["__lang_q"] = lang
    return items, res.get("total_count", 0) if isinstance(res, dict) else 0


def language_match(repo, ranks):
    r = ranks.get(repo["__lang_q"])
    return 30 if r == 0 else 24 if r in (1, 2) else 15 if r in (3, 4) else 6


def topic_match(repo, interests):
    topics = {t.lower() for t in (repo.get("topics") or [])}
    if not topics or not interests:
        return None
    return 25 * min(len(topics & interests), 3) / 3


def community_fit(repo, target):
    d = math.log10(repo["stargazers_count"] + 1) - math.log10(target + 1)
    return 20 * math.exp(-(d ** 2) / 0.5)


def complexity_ratio(repo, median_kb):
    """Wersja pierwotna ze specu, zostawiona do porównania."""
    r = repo["size"] / max(median_kb, 100)
    return 25 if 0.3 <= r <= 3 else 15 if 0.1 <= r <= 10 else 6


def complexity_pct(repo, pool_sizes):
    """Percentyl w puli kandydatów. Wersja, która weszła do specu."""
    smaller = sum(1 for s in pool_sizes if s > repo["size"])
    return 25 * smaller / max(len(pool_sizes) - 1, 1)


def renorm(parts):
    got = [(p, m) for p, m in parts if p is not None]
    if not got:
        return None
    return 100 * sum(p for p, _ in got) / sum(m for _, m in got)


def confidence(parts):
    tot = sum(m for _, m in parts)
    got = sum(m for p, m in parts if p is not None)
    return got / tot if tot else 0.0


def shrink(score, conf):
    """Ściąga wynik do neutralnych 50 proporcjonalnie do braków danych."""
    return None if score is None else 50 + (score - 50) * conf


LAT_OLD = [(48, 25), (24 * 7, 20), (24 * 30, 12)]
LAT_NEW = [(2, 25), (12, 19), (48, 13), (24 * 7, 7)]


def bucket(v, table, floor):
    for lim, pts in table:
        if v <= lim:
            return pts
    return floor


def health(full_name):
    prs = gh(f"/repos/{full_name}/pulls?state=all&per_page=30&sort=updated")
    opn = gh(f"/repos/{full_name}/pulls?state=open&sort=created&direction=asc&per_page=100")
    cli = gh(f"/repos/{full_name}/issues?state=closed&per_page=30")
    out = {}

    resolved = [p for p in prs if not p.get("draft") and p.get("closed_at")] \
        if isinstance(prs, list) else []
    if len(resolved) >= 5:
        merged = sum(1 for p in resolved if p.get("merged_at"))
        out["merge"] = 25 * merged / len(resolved)
        hrs = sorted(((parse_dt(p.get("merged_at") or p["closed_at"]) - parse_dt(p["created_at"])).total_seconds() / 3600)
                     for p in resolved)
        med = st.median(hrs)
        out["__lat_h"] = med
        out["lat_old"] = bucket(med, LAT_OLD, 4)
        out["lat_new"] = bucket(med, LAT_NEW, 2)
    else:
        out["merge"] = out["lat_old"] = out["lat_new"] = None

    if isinstance(opn, list) and opn:
        cut = NOW - timedelta(days=90)
        stale = sum(1 for p in opn if parse_dt(p["created_at"]) < cut)
        out["stale"] = 20 * (1 - stale / len(opn))
        # 100 zwróconych PR-ów znaczy, że widzimy tylko najstarsze i próbka jest skrzywiona
        out["__sampled"] = len(opn) == 100
        out["__stale_pct"] = 100 * stale / len(opn)
    else:
        out["stale"] = None
        out["__sampled"] = False
        out["__stale_pct"] = None

    if isinstance(cli, list):
        real = [i for i in cli if "pull_request" not in i and i.get("closed_at")]
        if real:
            days = sorted((parse_dt(i["closed_at"]) - parse_dt(i["created_at"])).days for i in real)
            out["__turn_d"] = st.median(days)
            out["turn"] = bucket(out["__turn_d"], [(7, 15), (30, 11), (90, 6)], 2)
        else:
            out["turn"] = None
    else:
        out["turn"] = None
    return out


def free_issues(full_name):
    lab = urllib.parse.quote("good first issue")
    iss = gh(f"/repos/{full_name}/issues?labels={lab}&state=open&per_page=20")
    if not isinstance(iss, list):
        return 0
    return sum(1 for i in iss if "pull_request" not in i and not i.get("assignees"))


def hr(title):
    print(f"\n\033[1m{title}\033[0m")
    print("-" * 78)


def spread(name, vals, maxpts):
    vals = [v for v in vals if v is not None]
    if not vals:
        print(f"  {name:<22} BRAK DANYCH")
        return
    lo, hi, med = min(vals), max(vals), st.median(vals)
    rng = hi - lo
    bar = "#" * int(round(rng / maxpts * 30))
    flag = "MARTWY" if rng < maxpts * 0.15 else "słaby " if rng < maxpts * 0.35 else "OK    "
    print(f"  {name:<22} min={lo:5.1f} med={med:5.1f} max={hi:5.1f}  "
          f"rozstrzał={rng:5.1f}/{maxpts}  {flag} {bar}")


ap = argparse.ArgumentParser()
ap.add_argument("--langs", default="C#,TypeScript,Python")
ap.add_argument("--target", type=int, default=500)
ap.add_argument("--login", default=os.environ.get("GH_LOGIN", "octocat"))
ap.add_argument("--finalists", type=int, default=25)
ap.add_argument("--workers", type=int, default=8)
a = ap.parse_args()

langs = [x.strip() for x in a.langs.split(",")]
ranks = {l: i for i, l in enumerate(langs)}
lo, hi = max(100, a.target // 5), a.target * 5
pushed = (NOW - timedelta(days=90)).strftime("%Y-%m-%d")

hr(f"KALIBRACJA  języki={langs}  target={a.target}  pasmo={lo}..{hi}  workers={a.workers}")

prof = profile(a.login)
print(f"profil {a.login}: {prof['n']} własnych repo, mediana {prof['median_kb']} KB, "
      f"{len(prof['interests'])} topików")

pool, totals = [], {}
for l in langs:
    items, tc = candidates(l, lo, hi, pushed)
    totals[l] = tc
    pool += items
    time.sleep(2.2)  # limit search: 30 zapytań na minutę
seen, uniq = set(), []
for r in pool:
    if r["full_name"] not in seen:
        seen.add(r["full_name"])
        uniq.append(r)
print("kandydaci:", ", ".join(f"{l}={totals[l]}" for l in langs),
      f"-> pobrano {len(pool)}, unikalnych {len(uniq)}")

sizes = [r["size"] for r in uniq]
for r in uniq:
    r["c_lang"] = language_match(r, ranks)
    r["c_topic"] = topic_match(r, prof["interests"])
    r["c_comm"] = community_fit(r, a.target)
    r["c_ratio"] = complexity_ratio(r, prof["median_kb"])
    r["c_pct"] = complexity_pct(r, sizes)
    r["match_old"] = renorm([(r["c_lang"], 30), (r["c_topic"], 25), (r["c_ratio"], 25), (r["c_comm"], 20)])
    r["match_new"] = renorm([(r["c_lang"], 30), (r["c_topic"], 25), (r["c_pct"], 25), (r["c_comm"], 20)])

hr("MATCH: rozkład komponentów w puli kandydatów")
spread("Language (30)", [r["c_lang"] for r in uniq], 30)
spread("Topic (25)", [r["c_topic"] for r in uniq], 25)
spread("Complexity ratio (25)", [r["c_ratio"] for r in uniq], 25)
spread("Complexity pct (25)", [r["c_pct"] for r in uniq], 25)
spread("Community (20)", [r["c_comm"] for r in uniq], 20)
print(f"  bez topics -> null: {sum(1 for r in uniq if r['c_topic'] is None)}/{len(uniq)}")
spread("MATCH stary", [r["match_old"] for r in uniq], 100)
spread("MATCH nowy", [r["match_new"] for r in uniq], 100)

uniq.sort(key=lambda r: r["match_new"], reverse=True)
fin = uniq[:a.finalists]

t0 = time.time()
with ThreadPoolExecutor(max_workers=a.workers) as ex:
    frees = list(ex.map(lambda r: free_issues(r["full_name"]), fin))
surv = [r for r, f in zip(fin, frees) if f > 0]
for r, f in zip(fin, frees):
    r["free"] = f
print(f"\nlejek: {len(surv)}/{len(fin)} finalistów ma wolne issue")

with ThreadPoolExecutor(max_workers=a.workers) as ex:
    hs = list(ex.map(lambda r: health(r["full_name"]), surv))
wall = time.time() - t0
for r, h in zip(surv, hs):
    r["h"] = h
    parts_old = [(h["merge"], 25), (h["lat_old"], 25), (h["stale"], 20), (15, 15), (h["turn"], 15)]
    parts_new = [(h["merge"], 25), (h["lat_new"], 25), (h["stale"], 20), (15, 15), (h["turn"], 15)]
    r["conf"] = confidence(parts_new)
    r["health_old"] = renorm(parts_old)
    r["health_new"] = renorm(parts_new)
    r["health_shr"] = shrink(r["health_new"], r["conf"])
    for k in ("old", "new", "shr"):
        m = r["match_new"] if k == "shr" else r[f"match_{k}"]
        hh = r[f"health_{k}"]
        r[f"final_{k}"] = 0.65 * m + 0.35 * hh if hh is not None else m

hr("HEALTH: rozkład komponentów u ocalałych")
spread("Merge rate (25)", [r["h"]["merge"] for r in surv], 25)
spread("Latency stare progi", [r["h"]["lat_old"] for r in surv], 25)
spread("Latency nowe progi", [r["h"]["lat_new"] for r in surv], 25)
spread("Stale (20)", [r["h"]["stale"] for r in surv], 20)
spread("Turnaround (15)", [r["h"]["turn"] for r in surv], 15)
lat = sorted(r["h"]["__lat_h"] for r in surv if r["h"].get("__lat_h") is not None)
if lat:
    print(f"  mediany latencji (h): p25={lat[len(lat)//4]:.0f} p50={st.median(lat):.0f} "
          f"p75={lat[3*len(lat)//4]:.0f} max={lat[-1]:.0f}")
print(f"  probkowany stale: {sum(1 for r in surv if r['h']['__sampled'])}/{len(surv)}")
spread("HEALTH stary", [r["health_old"] for r in surv], 100)
spread("HEALTH nowy", [r["health_new"] for r in surv], 100)
spread("HEALTH ściągnięty", [r["health_shr"] for r in surv], 100)
spread("FINAL stary", [r["final_old"] for r in surv], 100)
spread("FINAL nowy", [r["final_new"] for r in surv], 100)
spread("FINAL ściągnięty", [r["final_shr"] for r in surv], 100)
print(f"  komplet komponentów Health: {sum(1 for r in surv if r['conf'] > 0.99)}/{len(surv)}, "
      f"mediana pewności {st.median([r['conf'] for r in surv]):.0%}")

hr("TOP 10 według wersji nowej")
surv.sort(key=lambda r: r["final_new"], reverse=True)
print(f"  {'repo':<38}{'stars':>7}{'KB':>9}{'fin':>6}{'mat':>6}{'hlth':>6}{'pewn':>6}{'iss':>5}  język")
for r in surv[:10]:
    h = r["health_new"]
    print(f"  {r['full_name']:<38}{r['stargazers_count']:>7}{r['size']:>9}"
          f"{r['final_new']:>6.1f}{r['match_new']:>6.1f}"
          f"{(f'{h:.1f}' if h is not None else '  -'):>6}{r['conf']:>6.0%}{r['free']:>5}  {r['__lang_q']}")

top_new = {x["full_name"] for x in surv[:10]}
moved = sorted(surv, key=lambda r: r["final_shr"], reverse=True)[:10]
delta = [r["full_name"] for r in moved if r["full_name"] not in top_new]
print(f"  po ściągnięciu wchodzi do TOP10: {delta or 'bez zmian'}")

hr("KOSZT")
print(f"  wywołań: {_calls['n']}, średnia latencja: {_calls['ms']/_calls['n']:.0f} ms, "
      f"faza równoległa: {wall:.1f} s przy {a.workers} wątkach")
