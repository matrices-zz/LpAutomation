EF Core Migrations (CLI)

Add migration:
dotnet ef migrations add <Name> --project .\LpAutomation.Server\LpAutomation.Server.csproj --startup-project .\LpAutomation.Server\LpAutomation.Server.csproj

Update database:
dotnet ef database update --project .\LpAutomation.Server\LpAutomation.Server.csproj --startup-project .\LpAutomation.Server\LpAutomation.Server.csproj

List migrations:
dotnet ef migrations list --project .\LpAutomation.Server\LpAutomation.Server.csproj --startup-project .\LpAutomation.Server\LpAutomation.Server.csproj
