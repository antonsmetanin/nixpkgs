{ buildDotnetModule, dotnet-sdk }:
let
  text = ''<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include=\"Microsoft.DotNet.Cli.Sln.Internal\">
      <HintPath>${dotnet-sdk}/sdk/${dotnet-sdk.version}/Microsoft.DotNet.Cli.Sln.Internal.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>'';
in buildDotnetModule {
  pname = "csproj-reference-patcher";
  version = "1.0.0";
  projectFile = "patcher.csproj";
  src = ./.;
  nugetDeps = ./deps.nix;
  patchPhase = ''
  	echo -n "${text}" > "./patcher.csproj"
  '';
}