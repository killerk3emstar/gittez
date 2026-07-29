
# Gittez

GitHub ma miliony repozytoriów. Gittez pokazuje te kilka, do których warto
zacząć kontrybutować w tym tygodniu.

Zadanie rekrutacyjne CetusPro, program praktyk i staży 2026, ścieżka full stack.
Czas realizacji około 1,5 dnia.

Demo: `https://gittez-web-production.up.railway.app/`

<img width="1452" height="902" alt="Screenshot 2026-07-29 at 11 50 39 PM" src="https://github.com/user-attachments/assets/2e9b657e-9921-481e-8add-a7f714946b5e" />

<img width="1451" height="899" alt="Screenshot 2026-07-29 at 11 53 16 PM" src="https://github.com/user-attachments/assets/a5f024c8-0d28-4201-8295-f7184733d922" />


## Problem

Każdy, kto próbował wejść w open source, zna ten przepływ. GitHub Explore, lista
„good first issues", klikam pierwsze repozytorium, issue ma 47 komentarzy i jest
przypisane komuś od trzech miesięcy. Klikam drugie, maintainer nie odpowiedział
na żaden PR od pół roku. Zamykam kartę.

W 2026 doszedł drugi problem, gorszy. Odpaliłem docelowe zapytanie do GitHuba i
przeczytałem wyniki. Na 100 trafień z labelem `good first issue` w C# realnych
projektów było około dziesięciu. Reszta to repozytoria z wygenerowanym backlogiem:
zgłoszenia w stylu „Add a fictional IFR clearance readback test fixture", prefiks
`[Good First Issue]` doklejany ręcznie, organizacja nazwana `VibecodingGermany`.
Backlog produkuje się dziś w minutę, więc repozytorium potrafi wyglądać na żywe,
nie będąc żywym.

Brakuje więc nie samych issues z labelem, a filtra jakości: które repozytorium
faktycznie żyje, maintainer odpowiada, issue nie zgniło, a technologia jest moja.
Gittez jest tym filtrem. Pomysł nie jest przypadkowy, bo sam kontrybutowałem do
OSS (PR do [Wulkanowego](https://github.com/wulkanowy/wulkanowy)) i pamiętam, ile
czasu zajęło znalezienie projektu, w którym ktokolwiek odpowie.

## Uruchomienie

```bash
git clone <adres repozytorium>
cd gittez
cp .env.example .env      # wklej GITHUB_TOKEN i POSTGRES_PASSWORD
docker compose up
```

Aplikacja: http://localhost:8080. Dokumentacja API (Scalar):
http://localhost:8081/scalar, za flagą `ENABLE_API_DOCS` włączoną w Compose
domyślnie.

Demo działa bez tokenu, analiza własnego loginu wymaga tokenu. Repozytorium
zawiera seed cache'u (50 repozytoriów i 205 issues), więc aplikacja pokazuje
sensowne dane od razu po starcie. Bez tokenu GitHub daje 60 zapytań na godzinę, a
jeden świeży przebieg zużywa około stu, czyli nie starczy nawet na jeden. Token
podnosi limit do 5000 na godzinę i włącza analizę dowolnego profilu.

Na wdrożeniu obrazy budują się w GitHub Actions i lądują w GHCR, a na Railwayu
stoją trzy usługi: Postgres, `api` bez domeny publicznej i `web` z domeną
publiczną. Przeglądarka widzi wyłącznie `web`, a nginx proxuje `/api` po sieci
prywatnej. Front i API zostają dzięki temu na jednym originie, więc backend nie
ma i nie potrzebuje konfiguracji CORS, a Scalar nie wychodzi na zewnątrz
przypadkiem.

## Jak to działa

1. Podajesz swój login GitHub. Aplikacja analizuje Twoje publiczne repozytoria
   oraz projekty, do których kontrybutowałeś.
2. Pokazuje wykryte języki jako chipy z etykietą źródła („C#, 7 repo, 2
   kontrybucje"). Odznaczasz, dokładasz, decydujesz sam.
3. Szuka repozytoriów, które mają realne, wolne issues w Twoich językach.
4. Liczy dwa niezależne wyniki: Match Score (0 do 100), czyli jak bardzo repo
   pasuje do Ciebie, oraz Health Score (0 do 100), czyli czy repo jest żywe
   niezależnie od tego, kim jesteś.
5. Pokazuje 10 rekomendacji, każdą z przyciskiem „Dlaczego?" i pełnym rozbiciem
   obu wyników.
6. Ciekawe repozytorium zapisujesz na watchlistę z własną notatką.

## Scoring jest jawny

Każdy wynik to suma ważona komponentów, a każdy komponent niesie własne
wyjaśnienie powstające w tym samym miejscu, w którym powstaje liczba. Dzięki temu
opis nigdy nie rozjedzie się z wartością.

### Match Score

| Komponent | Waga | Na czym oparty |
|-----------|------|----------------|
| Dopasowanie języka | 30 | pozycja języka repo w Twoim rankingu (własne repo plus kontrybucje) |
| Dopasowanie tematyki | 25 | część wspólna `topics` repo i Twoich zainteresowań; `null`, gdy repo nie ma uzupełnionych topików |
| Przystępność kodu | 25 | percentyl rozmiaru repo w puli kandydatów, „mniejsze niż 78% z nich" |
| Wielkość społeczności | 20 | gaussian w skali logarytmicznej wokół preferowanego rzędu wielkości, który steruje też samym wyszukiwaniem |

### Health Score

| Komponent | Waga | Na czym oparty |
|-----------|------|----------------|
| Merge rate | 25 | odsetek PR-ów zmergowanych wobec zamkniętych bez merge |
| Czas rozstrzygnięcia PR | 25 | mediana czasu od otwarcia do zamknięcia lub merge |
| Odsetek zastałych PR-ów | 20 | otwarte PR-y starsze niż 90 dni |
| Aktywność | 15 | czas od ostatniego pusha |
| Czas zamykania issues | 15 | mediana z 30 ostatnich zamkniętych |

Komponenty, dla których brakuje danych, zwracają `null` i są opisane w UI jako
„za mało danych", a wynik procentuje się po dostępnych. Zera nie zgadujemy tam,
gdzie nie wiemy.

Wyniku końcowego nie ma na karcie jako liczby, służy wyłącznie do ustalenia
kolejności. Zmierzyłem, że w widocznej dziesiątce wyniki mieszczą się w 6,8
punktu, więc wielka cyfra sugerowałaby precyzję, której tam nie ma. Karta niesie
za to jedno zdanie z komponentu, który najmocniej wyróżnia dane repozytorium:
„mniejsze niż 78% kandydatów", „96% PR-ów zmergowanych", „3 wspólne tematy".
Nawet gdy rekomendacja nie trafi idealnie, użytkownik widzi, dlaczego ją dostał.

## Co zmienił pomiar przed napisaniem kodu

Zanim powstała pierwsza linijka, odpaliłem docelowe zapytania `curl`em i
przepuściłem cały pipeline przez żywe API dla dwóch zestawów językowych (300 i
257 kandydatów). Pełny zapis jest w [`docs/SPEC.md`](docs/SPEC.md) §0, tu trzy
rzeczy, które to zmieniło.

**Kierunek wyszukiwania.** Plan zakładał start od wyszukiwania issues, żeby
zrobić jedno zapytanie zamiast setek. Taki zestaw okazał się zdominowany przez
spam, a przyczyna siedziała w parametrze sortowania: `sort=updated&order=desc`
premiuje repozytoria ruszane najczęściej, a repo z generowanym backlogiem jest
ruszane bez przerwy, podczas gdy `npgsql/efcore.pg` ma issue sprzed trzech
tygodni. Mój parametr sortowania aktywnie selekcjonował to, co chciałem odsiać.
Kwalifikator `good-first-issues:>=n` w wyszukiwaniu repozytoriów rozwiązał to
razem z problemem jajka i kury przy filtrach jakości: bramka działa po stronie
GitHuba, odpowiedź ma komplet metadanych, a z budżetu znika 30 wywołań.

**Komponent „świeżość issues" wyleciał w całości.** Liczba pasujących issues
okazała się odwrotnie skorelowana z jakością: `jchable/okf4net` miał ich
dwanaście, `MudBlazor` i `dotnet/maui` po dwa, `unoplatform/uno` jedno. Dojrzały
projekt ma w danej chwili jedno albo dwa wolne good first issues, bo ludzie je
rozbierają. Mój komponent był rosnący (`min(count, 3) / 3 · 15 pkt`), więc
karałem projekty za to, że ludzie biorą ich zadania. Obniżenie go do 5 punktów
było połową kroku, bo skoro krok filtrowania i tak odrzuca repozytoria bez wolnego
issue, każdy kandydat dożywający rankingu dostawał komplet punktów. Gwarancję, że
jest w co kliknąć, daje teraz filtr, komunikowany raz nad listą: „wszystkie
wyniki mają co najmniej jedno nieprzypisane issue". Piętnaście punktów rozeszło
się na tematykę, złożoność i wielkość społeczności.

**Ten sam błąd popełniłem drugi raz, w drugą stronę.** Po przejściu na wyszukiwanie
repozytoriów odpaliłem `sort=stars&per_page=30` i dostałem 30 realnych projektów,
zero spamu, rozstrzał od 5 542 do 55 079 gwiazdek i dziewięć pozycji z organizacji
`dotnet`. Bramka jakości zadziałała idealnie, a scoring przestał działać
kompletnie: Community Fit od 2,3 do 0,0 punktu na 20, Complexity Match po 5
punktów wszystkim, Language Match 30 z 30 wszystkim, bo język jest kluczem
zapytania. Zostało 30 punktów ze stu, którymi te karty się od siebie różniły,
czyli lista popularnych repozytoriów w C#, którą GitHub Trending daje za darmo.
Naprawa: `targetStars` wchodzi do zapytania jako log-symetryczne pasmo
`stars:{target/5}..{target·5}`, dla domyślnych 500 gwiazdek `stars:100..2500`, a
parametr `sort` znika, bo kolejność ma ustalać mój Match Score. Suwak przestał
być przy tym ozdobnikiem: przesunięcie go zwraca inne repozytoria, a nie te same
karty z przeliczonymi punktami.

Zmierzyłem też rozstrzał każdego komponentu, bo taki, który wszystkim daje
podobnie, w interfejsie opartym na wytłumaczalności jest bezwartościowy. Dwa
okazały się martwe. Złożoność liczona jako stosunek do mediany moich repozytoriów
dawała minimum ponad połowie kandydatów (44 MB mediany w puli przy 337 KB moich
projektów), więc komponent liczy teraz percentyl w puli. Progi czasu
rozstrzygnięcia PR dawały komplet punktów wszystkiemu poniżej 48 godzin, przy
zmierzonej medianie 2 do 3 godzin.

Wspólny mianownik: parametr sortowania kształtuje wynik mocniej niż filtry. Filtry
mówią, co jest dopuszczalne, sortowanie decyduje, co faktycznie zobaczysz w
pierwszej setce. Za pierwszym razem dało mi to spam, za drugim same giganty, więc
ostatecznie nie sortuję po stronie GitHuba wcale.

## Stack

Backend: .NET 10, ASP.NET Core Minimal API, EF Core 10, PostgreSQL 16, Scalar.
Frontend: React 19, TypeScript, Vite, TanStack Query, Tailwind. Infrastruktura:
Docker Compose lokalnie, GitHub Actions z GHCR i Railway na wdrożeniu.

Podział na projekty: `Core` (logika, zero zależności zewnętrznych),
`Infrastructure` (EF, GitHub), `Api` (endpointy), `Tests`. Scoring żyje w `Core`
jako czyste funkcje, stąd testy bez bazy i bez sieci.

.NET 10 zamiast 9 świadomie: 9 jest w fazie maintenance z końcem wsparcia w
listopadzie 2026, a 10 to LTS do 2028. Koszt wyboru to jedna linijka.

## Decyzje projektowe

**Brak logowania przez OAuth.** Wszystkie potrzebne dane są publiczne, a OAuth
oznaczałby, że każdy, kto chce zobaczyć demo, musi najpierw autoryzować obcą
aplikację na swoim koncie GitHub. Tożsamość watchlisty to anonimowy UUID w
`localStorage`, który nie jest uwierzytelnieniem i nie chroni niczego wrażliwego.
Przy wejściu na produkcję OAuth wróciłby razem z danymi prywatnymi.

**Języki z dwóch źródeł.** REST daje języki własnych repozytoriów, ale merged PR
do cudzego projektu mówi o umiejętnościach nie mniej niż własne repo. GraphQL to
udostępnia: `repositoriesContributedTo` zwraca jednym zapytaniem języki razem z
rozkładem bajtów. Kontrybucja waży w rankingu tyle co własne repozytorium.

**Health Score liczony dopiero dla finalistów.** Ocena zdrowia to 3 zapytania na
repozytorium. Match liczę dla wszystkich około stu kandydatów za darmo, bo
metadane przyszły z wyszukiwania, ale Health tylko dla 25 najlepiej dopasowanych,
więc bardzo zdrowe repozytorium ze słabym dopasowaniem nadal nie wypłynie.
Dwadzieścia pięć zamiast dwudziestu, bo filtr wolnych issues zwęża lejek, a na
wyjściu musi zostać dziesiątka.

**Współbieżność ograniczona świadomie.** Jeden przebieg to około stu wywołań HTTP
(zmierzone 101 i 104), więc wąskim gardłem jest latencja, a nie limit zapytań.
Wywołania per-repozytorium idą przez `Parallel.ForEachAsync` z
`MaxDegreeOfParallelism = 8`, a podniesienie tego do 16 wątków nie skróciło
niczego: pojedyncze wywołanie trwa około 530 ms, ale równolegle latencja rośnie
do 1,1 do 1,6 s, bo GitHub dławi współbieżność. Pierwszy przebieg na zimnym
cache'u zajmuje z tego powodu około 19 sekund lokalnie i 21 sekund na wdrożeniu
(`limit=3`, pomiar z 29 lipca), przy 0,13 s na statykę. Kolejne idą z cache'u i
schodzą do kilku wywołań, ale skeleton podczas ładowania nie jest tu ozdobnikiem.

**Rate limit z nagłówków, nie z endpointu.** `X-RateLimit-*` realnych odpowiedzi
mówi prawdę, a `/rate_limit` po 255 zużytych wywołaniach nadal raportował pełne
5000.

**Cache w PostgreSQL zamiast w Redisie.** Baza i tak jest potrzebna do
watchlisty, a Redis byłby drugą zależnością infrastrukturalną dla zysku
niemierzalnego przy tej skali. Odświeżanie używa ETagów, a odpowiedź 304 nie
zmniejsza limitu API.

**Seed ładowany po migracjach.** Naturalny odruch to zamontować `db/seed/` w
`/docker-entrypoint-initdb.d/`, co nie działa: skrypty initdb odpalają się przy
inicjalizacji pustego katalogu danych, czyli zanim API zaaplikuje migracje EF, a
`INSERT` trafiłby w nieistniejącą tabelę i przerwał start kontenera. Seed jest
więc idempotentnym `INSERT ... ON CONFLICT DO NOTHING` wołanym z `Program.cs` po
`MigrateAsync()`.

**Degradacja zamiast błędu.** Gdy limit GitHuba się wyczerpie, aplikacja serwuje
dane z cache'u niezależnie od TTL i oznacza je bannerem „dane sprzed X godzin".
503 zwracam wyłącznie wtedy, gdy cache jest pusty i nie mam czym poratować, bo
puste demo z komunikatem o błędzie byłoby gorsze niż lekko nieświeże dane.

**Słabe wyniki są pokazywane.** Repozytorium z niskim Health dostaje ostrzegawczą
plakietkę zamiast wypaść z listy. Dziesięć kart z wynikiem 80 do 86 wygląda jak
zepsuty licznik, a jedna karta z „Health 34, ostatni push 7 miesięcy temu, 60%
otwartych PR-ów starszych niż 90 dni" jest dowodem, że ocena cokolwiek mierzy.

## Świadome ograniczenia

Rzeczy, których nie da się uzyskać z publicznego API w rozsądnym budżecie
zapytań, wypisane, żeby nie wyglądały na przeoczenia:

- **Złożoność liczona z rozmiaru repozytorium.** GitHub nie udostępnia liczby
  linii kodu, a pole `size` to rozmiar gita w KB razem z grafiką i testami.
- **Czas do pierwszego review zastąpiony czasem do rozstrzygnięcia PR-a**, bo
  pierwsze review wymagałoby osobnego zapytania dla każdego PR-a.
- **Odsetek zastałych PR-ów bywa próbką.** Pobieram 100 najstarszych otwartych
  PR-ów i przy większej liczbie wiem tylko, że jest ich co najmniej 100, a próbka
  jest z definicji najgorsza. Takie komponenty są w UI oznaczone. W domyślnym
  paśmie gwiazdek nie wystąpiło to ani raz na 49 finalistów.
- **Brak przypisania nie znaczy, że nikt nad issue nie pracuje.** Sporo zgłoszeń
  jest zaklepanych w komentarzu, a wykrycie tego wymagałoby pobierania komentarzy
  dla każdego issue.
- **Ocena trudności jest heurystyką** opartą na labelach, długości opisu i
  liczbie komentarzy, i tak jest opisana w UI.
- **Próg 100 gwiazdek odcina projekty, które dopiero startują.** Kompromis
  świadomy, bo poniżej tego progu pula jest zdominowana przez generowany spam.

## Testy

```bash
dotnet test
```

51 metod testowych, wszystkie bez sieci. Scoring pokrywają testy jednostkowe na
czystych funkcjach z `Core`, w tym przypadki brzegowe: klamrowanie dzielnika przy
pustym profilu, repozytorium ze zbyt małą liczbą PR-ów do oceny (`null` zamiast
zera), zero otwartych PR-ów (`null` zamiast maksimum), odfiltrowanie pull
requestów z listy issues (GitHub zwraca je w tym samym endpoincie), pominięcie
draftów i botów w merge rate, procentowanie obu wyników po dostępnych
komponentach.

Dwa testy pilnują błędów znalezionych w danych, a nie w kodzie: repozytorium bez
uzupełnionych `topics` dostaje `null` zamiast zera, inaczej traciłoby ćwiartkę
wyniku za zaniedbanie maintainera w metadanych; pasmo gwiazdek ma dolny próg 100,
bo bez pasma cała pula ląduje w przedziale od 5 do 55 tysięcy gwiazdek i
komponent wielkości społeczności zwraca zero dla wszystkiego.

Pipeline ma osobne testy scenariuszy awaryjnych (zerwane połączenie i wyczerpany
limit schodzą na cache, repozytorium które zniknęło wypada z listy zamiast
wywracać przebieg), a ścieżkę zapisu pokrywają testy integracyjne na
`WebApplicationFactory`: zapis, edycja notatki i usunięcie, 409 przy duplikacie,
niewidoczność pozycji innej sesji.

## Czego nie zdążyłem

- **Wyszukiwanie semantyczne.** `/search/issues` przyjmuje dziś
  `search_type=hybrid`, co pozwoliłoby dopasowywać treść issue do zainteresowań
  użytkownika, a nie tylko język i topics. Limit 10 zapytań na minutę i osobna
  kalibracja nie zmieściły się w budżecie czasowym.
- Powiadomienia o nowych issues w obserwowanych repozytoriach.
- Historia rekomendacji i oznaczanie „zrobione".
- Testcontainers. Testy jadą na SQLite w pamięci, więc zgodność mapowania `jsonb`
  z realnym Postgresem sprawdzałem ręcznie przeciw `docker compose`.
- Testy interakcji na froncie. `npm run smoke` renderuje wszystkie ekrany i
  sprawdza 34 asercje na treści HTML, ale nie dotyka kliknięć.

## Wykorzystanie AI

Zgodnie z prośbą w dokumencie rekrutacyjnym, z czego i do czego korzystałem:

| Obszar | Narzędzie | Zakres |
|--------|-----------|--------|
| Koncepcja i architektura | Claude Code | dyskusja o modelu scoringu, audyt wykonalności na podstawie dokumentacji GitHub API, weryfikacja limitów i wersji |
| Kod | Claude Code | boilerplate endpointów, komponenty React, konfiguracja Compose |
| Analiza błędów | Claude Code | debug integracji z API i zapytań EF |
| Dokumentacja | Claude Code | szkic README |

Wzory scoringu, wagi i progi ustaliłem sam. Wynikają z tego, co API faktycznie
zwraca, i były zmieniane po weryfikacji na realnych danych, co opisuje sekcja o
pomiarach.

## Autor

Bartosz Sławiński,
[GitHub](https://github.com/killerk3emstar),
[LinkedIn](https://linkedin.com/in/bartosz-slawinski)
