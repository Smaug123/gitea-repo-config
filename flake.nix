{
  description = "Declarative .NET Gitea configuration";

  inputs = {
    flake-utils.url = "github:numtide/flake-utils";
    nixpkgs.url = "nixpkgs/nixpkgs-unstable";
  };

  outputs = {
    self,
    nixpkgs,
    flake-utils,
    ...
  }:
    flake-utils.lib.eachDefaultSystem (system: let
      pkgs = nixpkgs.legacyPackages.${system};
      projectFile = "./Gitea.Declarative/Gitea.Declarative.fsproj";
      testProjectFile = "./Gitea.Declarative.Test/Gitea.Declarative.Test.fsproj";
      pname = "gitea-repo-config";
      dotnet-sdk = pkgs.dotnet-sdk_8;
      dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;
      version = "0.1";
      dotnetTool = toolName: toolVersion: sha256:
        pkgs.stdenvNoCC.mkDerivation rec {
          name = toolName;
          version = toolVersion;
          nativeBuildInputs = [pkgs.makeWrapper];
          src = pkgs.fetchNuGet {
            pname = name;
            version = version;
            sha256 = sha256;
            installPhase = ''mkdir -p $out/bin && cp -r tools/net6.0/any/* $out/bin'';
          };
          installPhase = ''
            runHook preInstall
            mkdir -p "$out/lib"
            cp -r ./bin/* "$out/lib"
            makeWrapper "${dotnet-runtime}/bin/dotnet" "$out/bin/${name}" --add-flags "$out/lib/${name}.dll"
            runHook postInstall
          '';
        };
    in let
      default = pkgs.buildDotnetModule {
        pname = pname;
        name = "gitea-repo-config";
        version = version;
        src = ./.;
        projectFile = projectFile;
        nugetDeps = ./nix/deps.nix; # `nix build .#default.passthru.fetch-deps && ./result` and put the result here
        doCheck = true;
        dotnet-sdk = dotnet-sdk;
        dotnet-runtime = dotnet-runtime;
      };
    in {
      packages = {
        fantomas = dotnetTool "fantomas" (builtins.fromJSON (builtins.readFile ./.config/dotnet-tools.json)).tools.fantomas.version "sha256-zYSF53RPbGEQt1ZBcHVYqEPHrFlmI1Ty3GQPW1uxPWw=";
        default = default;
      };
      apps = {
        default = {
          type = "app";
          program = "${self.packages.${system}.default}/bin/Gitea.Declarative";
        };
      };
      devShells = {
        default = pkgs.mkShell {
          buildInputs = with pkgs; [
            (with dotnetCorePackages;
              combinePackages [
                dotnet-sdk_8
                dotnetPackages.Nuget
              ])
          ];
          packages = [
            pkgs.alejandra
            pkgs.nodePackages.markdown-link-check
            pkgs.shellcheck
          ];
        };
      };
    });
}
