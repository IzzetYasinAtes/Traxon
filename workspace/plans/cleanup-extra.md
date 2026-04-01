## EK GÖREV: Admin projesi kaldırma

Admin projesi artık gereksiz (tüm sayfalar Dashboard'a taşındı). Yapılacak:

1. Traxon.slnx'ten Admin satırını kaldır:
   `<Project Path="src/CryptoTrader/Traxon.CryptoTrader.Admin/Traxon.CryptoTrader.Admin.csproj" />`

2. Admin proje klasörünü git rm ile sil:
   `git rm -r src/CryptoTrader/Traxon.CryptoTrader.Admin/`

3. Başka projeler Admin'e referans veriyor mu kontrol et (grep ile). Varsa kaldır.

4. Build test: `dotnet build Traxon.slnx`
