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
      dotnet-sdk = pkgs.dotnetCorePackages.sdk_9_0;
      dotnet-runtime = pkgs.dotnetCorePackages.runtime_9_0;
      version = "0.1";
      dotnetTool = toolName: toolVersion: hash:
        pkgs.stdenvNoCC.mkDerivation rec {
          name = toolName;
          version = toolVersion;
          nativeBuildInputs = [pkgs.makeWrapper];
          src = pkgs.fetchNuGet {
            pname = name;
            version = version;
            hash = hash;
            installPhase = ''mkdir -p $out/bin && cp -r tools/*/any/* $out/bin'';
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
        inherit version pname projectFile testProjectFile dotnet-sdk dotnet-runtime;
        name = "gitea-repo-config";
        src = ./.;
        nugetDeps = ./nix/deps.nix; # `nix build .#default.passthru.fetch-deps && ./result nix/deps.nix`
        doCheck = true;
      };
    in {
      packages = {
        fantomas = dotnetTool "fantomas" (builtins.fromJSON (builtins.readFile ./.config/dotnet-tools.json)).tools.fantomas.version (builtins.head (builtins.filter (elem: elem.pname == "fantomas") ((import ./nix/deps.nix) {fetchNuGet = x: x;}))).hash;
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
          buildInputs = [
            dotnet-sdk
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
