#!/bin/zsh

export GITEA_TOKEN="$1"

function execute() {
    target="$1"
    asciinema rec --overwrite --cols 100 --env "GITEA_TOKEN,SHELL,TERM" --command 'nix run github:Smaug123/gitea-repo-config -- \
    --config-file /tmp/GiteaConfig.json \
    --gitea-host=https://gitea.patrickstevens.co.uk \
    --gitea-admin-api-token "$GITEA_TOKEN" \
; echo -n ">" && sleep 1 && echo "\n> "' "$target.cast"

    cat "$target.cast" | ../node_modules/.bin/svg-term > "$target.svg"
}

execute "no-op"
