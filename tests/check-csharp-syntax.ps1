$ErrorActionPreference = "Stop"
$sdkVersion = (dotnet --version).Trim()
if ($LASTEXITCODE -ne 0) { throw "dotnet CLI unavailable" }
$dotnetRoot = if ($env:DOTNET_ROOT) { $env:DOTNET_ROOT } else {
    Split-Path (Get-Command dotnet).Source -Parent
}
$roslynDir = Join-Path $dotnetRoot "sdk\$sdkVersion\Roslyn\bincore"
$cs = Join-Path $roslynDir "Microsoft.CodeAnalysis.CSharp.dll"
$base = Join-Path $roslynDir "Microsoft.CodeAnalysis.dll"
if (!(Test-Path $cs) -or !(Test-Path $base)) {
    throw "Could not locate Roslyn parser in SDK $sdkVersion"
}
$temp = Join-Path $env:RUNNER_TEMP "RebornMenuSyntax"
dotnet new console -n Syntax -o $temp -f net10.0 --force
if ($LASTEXITCODE -ne 0) { throw "Could not prepare the syntax test" }
$proj = Join-Path $temp "Syntax.csproj"
$xml = Get-Content -Raw $proj
$xml = $xml.Replace("</Project>", @"
  <ItemGroup>
    <Reference Include="Microsoft.CodeAnalysis">
      <HintPath>$base</HintPath>
    </Reference>
    <Reference Include="Microsoft.CodeAnalysis.CSharp">
      <HintPath>$cs</HintPath>
    </Reference>
  </ItemGroup>
</Project>
"@)
Set-Content -Path $proj -Value $xml -Encoding utf8
@'
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

var root = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
    ?? throw new Exception("Use in GitHub Actions only.");
var sources = Directory.GetFiles(
    Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories);
if (sources.Length == 0) throw new Exception("No RebornMenu runtime sources found");
int totalErrors = 0;
foreach (var path in sources)
{
    var tree = CSharpSyntaxTree.ParseText(
        File.ReadAllText(path),
        new CSharpParseOptions(LanguageVersion.Latest), path);
    var errors = tree.GetDiagnostics().Where(
        x => x.Severity == DiagnosticSeverity.Error).ToList();
    foreach (var error in errors) Console.WriteLine(error);
    totalErrors += errors.Count;
    Console.WriteLine($"{Path.GetFileName(path)}: {errors.Count} syntax errors");
}
if (totalErrors != 0) throw new Exception($"Found {totalErrors} syntax errors");
Console.WriteLine("C# syntax validated. Runtime references still require local IL2CPP build.");
'@ | Set-Content -Path (Join-Path $temp "Program.cs") -Encoding utf8
dotnet run --project $proj -c Release
if ($LASTEXITCODE -ne 0) { throw "Syntax test failed" }
