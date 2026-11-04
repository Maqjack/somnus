Somnus {version}

Somnus usypia, hibernuje lub wyłącza komputer z Windowsem po odliczeniu czasu albo o wskazanej
godzinie, z 60-sekundowym ostrzeżeniem, w którym można zatrzymać odliczanie.

Autor: Mac (Maqjack). Strona i kod źródłowy: https://github.com/Maqjack/somnus


JAK UŻYWAĆ

1. Wybierz Odliczanie (po czasie) albo O której (o godzinie).
2. Wpisz liczbę i wybierz sekundy, minuty lub godziny albo wpisz godzinę i minutę. Kliknięcie
   lub Tab w polu zaznacza jego wartość, więc można od razu wpisać nową.
3. Wybierz Uśpij, Hibernuj lub Wyłącz. Przy wyłączeniu możesz zaznaczyć Wymuś zamknięcie
   programów; niezapisana praca w innych programach przepadnie.
4. Naciśnij Start (albo Enter). Okno chowa się do zasobnika; po najechaniu na półksiężyc widać
   pozostały czas.

W ostatnich 60 sekundach ostrzeżenie w prawym dolnym rogu ekranu daje do wyboru Stop (także
Esc) i Wykonaj teraz; minus tylko przenosi je na pasek zadań, gdy odliczanie trwa dalej.
Kliknięcie ikony w zasobniku pozwala zmienić lub zatrzymać trwające
odliczanie. Jeśli komputer wybudzi się na mniej niż minutę przed zaplanowanym momentem albo po
nim, Somnus anuluje odliczanie zamiast działać.

W trybie O której zaznacz Codziennie albo wybrane dni, a odliczanie będzie się powtarzać. Somnus
musi działać: włącz w ustawieniach Uruchamiaj z Windowsem albo uruchom go ponownie po wyłączeniu
komputera, a harmonogram wróci sam. Stop kończy harmonogram.

Koło zębate otwiera ustawienia: język (polski lub angielski), ostrzeżenie, zapamiętywanie
ostatniego odliczania, dźwięk ostatnich 20 sekund (kołysanka, bez dźwięku albo pierwsze 20 sekund
własnego pliku MP3, WAV, WMA, M4A, AAC lub FLAC) i uruchamianie z Windowsem.


WYMAGANIA

Windows 10 22H2 lub Windows 11, x64, oraz środowisko Microsoft .NET Desktop Runtime 10. Instalator
dodaje je, jeśli go brakuje, a Windows raz prosi wtedy o zgodę. Do wersji przenośnej zainstaluj je
ze strony https://dotnet.microsoft.com/download/dotnet/10.0. Sam Somnus nie potrzebuje uprawnień
administratora.


PRYWATNOŚĆ

Somnus nigdy nie korzysta z sieci, nie wysyła telemetrii i nie prosi o uprawnienia
administratora. Uruchamia się razem z Windowsem tylko wtedy, gdy włączysz to w ustawieniach. Tylko jego instalator łączy się z siecią,
żeby pobrać od Microsoftu środowisko .NET Desktop Runtime, jeśli komputer go nie ma. Ustawienia trzyma w
%APPDATA%\Somnus\settings.json, a dzienne logi przez 7 dni w %APPDATA%\Somnus\logs.


GDY COŚ NIE DZIAŁA

- „System Windows ochronił ten komputer”: Somnus nie jest podpisany certyfikatem. Porównaj sumę
  SHA256 pobranego pliku z SHA256SUMS.txt na stronie wydania, potem wybierz Więcej informacji
  i Uruchom mimo to.
- Hibernacja jest wyszarzona: hibernacja jest wyłączona w Windowsie. Uruchom
  „powercfg /hibernate on” w terminalu otwartym jako administrator, potem przywróć okno Somnusa.
- Uśpienie jest wyszarzone: komputer używa trybu Modern Standby, którego aplikacje nie mogą
  włączyć. Użyj hibernacji lub wyłączenia.
- Zwykłe wyłączenie nie nastąpiło: program z niezapisaną pracą je zatrzymał. Najpierw zapisz
  pracę albo zaznacz Wymuś zamknięcie programów.
- Coś innego: szczegóły są w %APPDATA%\Somnus\logs. Zgłoś problem na
  https://github.com/Maqjack/somnus/issues


ODINSTALOWANIE

Wersja zainstalowana: Ustawienia > Aplikacje > Zainstalowane aplikacje > Somnus > Odinstaluj;
usuwa to także ustawienia, logi i uruchamianie z Windowsem. Wersja przenośna: wyłącz w ustawieniach
Uruchamiaj z Windowsem, potem usuń folder Somnusa i %APPDATA%\Somnus.
Środowisko .NET Desktop Runtime zostaje, bo mogą go używać inne programy.


LICENCJA

Licencja MIT, copyright (c) 2026 Mac (Maqjack): zobacz LICENSE.txt, gdzie po polskim
tłumaczeniu jest wiążący tekst angielski. Licencje dołączonych komponentów są w
THIRD_PARTY_LICENSES.txt (po angielsku).
