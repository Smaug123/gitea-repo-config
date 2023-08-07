Demos taken with [asciinema](https://asciinema.org/).
(Yes, I have revoked the access token I used when recording these.)

# No-op

A no-op, where the configuration is already in sync with the remote Gitea instance ([raw cast](./casts/no-op.cast)):
![No-op update demo](./casts/no-op.svg)


# Repo creation

Create a repo by adding an appropriate entry in config ([raw cast](./casts/on-creation.cast)):
![Repo creation demo](./casts/on-creation.svg)

# Repo update

Make a change to that repo by editing some of its fields in the config file ([raw cast](./casts/update.cast)):
![Repo update demo](./casts/update.svg)

# Repo deletion

Delete the repo by setting its `"deleted"` field to `true` ([raw cast](./casts/deletion.cast)):
![Repo deletion demo](./casts/deletion.svg)

