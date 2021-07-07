///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var oloPackUrl = Argument("oloPackUrl", "");
var oloPackKey = Argument("oloPackKey", "");
var gitHubToken = Argument("GitHubApiToken", "");
var jiraAuthToken = Argument("jiraAuthToken", "");
var majorMinorVersion = "5.2";

string teamCityBuildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "0";
string GetBuildNumber() => $"{majorMinorVersion}.{teamCityBuildNumber}";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

TaskSetup(ctx =>
{
   if(TeamCity.IsRunningOnTeamCity)
   {
      TeamCity.WriteStartBuildBlock(ctx.Task.Name);
   }
});

TaskTeardown(ctx =>
{
   if(TeamCity.IsRunningOnTeamCity)
   {
      TeamCity.WriteEndBuildBlock(ctx.Task.Name);
   }
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Build Library")
.Does(() => {
   DotNetCoreBuild("OpenRasta.sln", new DotNetCoreBuildSettings {
      Configuration = configuration,
      
      ArgumentCustomization = args => 
               args
                  .Append($"/p:AssemblyVersion={GetBuildNumber()}")
                  .Append($"/p:AssemblyFileVersion={GetBuildNumber()}")
   });
});

Task("Run Unit Tests")
.IsDependentOn("Build Library")
.DoesForEach(new[] { "net461", "net48", "netcoreapp2.2" }, (framework) => {
   var settings = new DotNetCoreTestSettings
   {
      ArgumentCustomization = args => args.Append("-- NUnit.NumberOfTestWorkers=1"),
      Configuration = configuration,
      Framework = framework,
      NoBuild = true,
      NoRestore = true,
      Logger = $"trx;LogFileName=.\\TestOutput.{framework}.xml",
      Filter = "TestCategory!=Integration&" + 
               "TestCategory!=Database&" +
               "TestCategory!=Ignore&" +
               "TestCategory!=Slow&" +
               "TestCategory!=Flaky"
   };
   DotNetCoreTest($"src/OpenRasta.Tests.Unit/OpenRasta.Tests.Unit.csproj", settings);
   settings = new DotNetCoreTestSettings
   {
      ArgumentCustomization = args => args.Append("-- NUnit.NumberOfTestWorkers=1"),
      Configuration = configuration,
      Framework = framework,
      NoBuild = true,
      NoRestore = true,
      Logger = $"trx;LogFileName=.\\TestOutput.{framework}.xml",
      Filter = "TestCategory!=Integration&" + 
               "TestCategory!=Database&" +
               "TestCategory!=Ignore&" +
               "TestCategory!=Slow&" +
               "TestCategory!=Flaky"
   };
   DotNetCoreTest($"src/OpenRasta.DI.Windsor.Tests.Unit/OpenRasta.DI.Windsor.Tests.Unit.csproj", settings);
})
.DeferOnError()
.Finally(() =>
{
   if (TeamCity.IsRunningOnTeamCity) {
      foreach (var file in GetFiles("tests\\TestResults\\.\\TestOutput*.xml")) {
         TeamCity.ImportData("mstest", file.ToString());
      }
   }
});

Task("Package OpenRasta Library")
.IsDependentOn("Build Library")
.Does(() => {
   var settings = new DotNetCorePackSettings()
   {
      Configuration = configuration,
      NoBuild = true,
      OutputDirectory = ".",
      NoRestore = true,
      IncludeSource = true,
      IncludeSymbols = true,
      ArgumentCustomization = args => args.Append($"/p:PackageVersion={GetBuildNumber()}")
   };
   DotNetCorePack("src/OpenRasta/OpenRasta.csproj", settings);
   settings = new DotNetCorePackSettings()
   {
      Configuration = configuration,
      NoBuild = true,
      OutputDirectory = ".",
      NoRestore = true,
      IncludeSource = true,
      IncludeSymbols = true,
      ArgumentCustomization = args => args.Append($"/p:PackageVersion={GetBuildNumber()}-prerelease")
   };
   DotNetCorePack("src/OpenRasta/OpenRasta.csproj", settings);
});

Task("Package Windsor Library")
.IsDependentOn("Build Library")
.Does(() => {
   var settings = new DotNetCorePackSettings()
   {
      Configuration = configuration,
      NoBuild = true,
      OutputDirectory = ".",
      NoRestore = true,
      IncludeSource = true,
      IncludeSymbols = true,
      ArgumentCustomization = args => args.Append($"/p:PackageVersion={GetBuildNumber()}")
   };
   DotNetCorePack("src/OpenRasta.DI.Windsor/OpenRasta.DI.Windsor.csproj", settings);
   settings = new DotNetCorePackSettings()
   {
      Configuration = configuration,
      NoBuild = true,
      OutputDirectory = ".",
      NoRestore = true,
      IncludeSource = true,
      IncludeSymbols = true,
      ArgumentCustomization = args => args.Append($"/p:PackageVersion={GetBuildNumber()}-prerelease")
   };
   DotNetCorePack("src/OpenRasta.DI.Windsor/OpenRasta.DI.Windsor.csproj", settings);
});

Task("Package Katana Library")
.IsDependentOn("Build Library")
.Does(() => {
   var settings = new DotNetCorePackSettings()
   {
      Configuration = configuration,
      NoBuild = true,
      OutputDirectory = ".",
      NoRestore = true,
      IncludeSource = true,
      IncludeSymbols = true,
      ArgumentCustomization = args => args.Append($"/p:PackageVersion={GetBuildNumber()}")
   };
   DotNetCorePack("src/OpenRasta.Hosting.Katana/OpenRasta.Hosting.Katana.csproj", settings);
   settings = new DotNetCorePackSettings()
   {
      Configuration = configuration,
      NoBuild = true,
      OutputDirectory = ".",
      NoRestore = true,
      IncludeSource = true,
      IncludeSymbols = true,
      ArgumentCustomization = args => args.Append($"/p:PackageVersion={GetBuildNumber()}-prerelease")
   };
   DotNetCorePack("src/OpenRasta.Hosting.Katana/OpenRasta.Hosting.Katana.csproj", settings);
});


Task("clean")
.Does(() => {
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("Set TeamCity Build Number")
.WithCriteria(() => TeamCity.IsRunningOnTeamCity)
.Does(() => {
   BuildSystem.TeamCity.SetBuildNumber(GetBuildNumber());
});

#addin nuget:?package=Cake.Http&version=0.7.0
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2
JObject PerformJiraRequest(string endpoint)
{
   var settings = new HttpSettings
   {
      Headers = new Dictionary<string, string>
      {
            { "Authorization", $"Basic {jiraAuthToken}" },
            { "Cache-Control", "no-store" },
            { "Connection", "keep-alive" }
      }
   };
   Information($"https://ololabs.atlassian.net/rest/api/3/{endpoint}");
   return ParseJson(HttpGet($"https://ololabs.atlassian.net/rest/api/3/{endpoint}", settings)); 
}

Task("Generate Release Notes")
.Does(() => {
   #addin nuget:?package=Cake.FileHelpers&version=3.2.1
   var result = PerformJiraRequest($"project/FOUND/version?query=of-v{majorMinorVersion}&status=unreleased");
   var release = result["values"].FirstOrDefault();
   if (release != null)
   {
      var issues = PerformJiraRequest($"search?jql=fixVersion%20%3D%20{release["name"]}&fields=key,summary,issuetype");
      FileWriteLines("./releasenotes.md",
         issues["issues"]
            .GroupBy(x => x["fields"]["issuetype"]["name"])
            .SelectMany(grouping => new[] { $"# {grouping.Key}" }.Concat(grouping.OrderBy(x => x["key"]).Select(x => $"* [{x["key"]}](https://ololabs.atlassian.net/browse/{x["key"]}) - {x["fields"]["summary"]}")))
            .ToArray());
   }
   else
   {
      FileWriteLines("./releasenotes.md", new [] { "<Fill this in>" });
   }
});

Task("CreateGithubRelease")
.IsDependentOn("Generate Release Notes")
.Does(() => {
   #tool nuget:?package=GitReleaseManager&version=0.8.0
   var settings = new GitReleaseManagerCreateSettings {
      Name              = $"v{GetBuildNumber()}",
      Prerelease        = false,
      TargetCommitish   = "master",
      InputFilePath     = "./releasenotes.md",
      TargetDirectory   = ".",
   };
   GitReleaseManagerCreate(gitHubToken, "ololabs", "openrasta-core", settings);
});

Task("default")
.IsDependentOn("Set TeamCity Build Number")
.IsDependentOn("Build Library")
.IsDependentOn("Run Unit Tests")
.IsDependentOn("Package OpenRasta Library")
.IsDependentOn("Package Windsor Library")
.IsDependentOn("Package Katana Library")
.Does(() => {
});

RunTarget(target);
