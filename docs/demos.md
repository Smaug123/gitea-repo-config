Demos taken with [asciinema](https://asciinema.org/).

# No-op

A no-op, where the configuration is already in sync with the remote Gitea instance ([raw cast](./no-op.cast)):
![No-op update demo](./no-op.svg)


# Repo creation

Create a repo by adding an appropriate entry in config ([raw cast](./on-creation.cast)):
![Repo creation demo](./on-creation.svg)

# Repo update

Make a change to that repo by editing some of its fields in the config file ([raw cast](./update.cast)):
![Repo update demo](./update.svg)

# Repo deletion

Delete the repo by setting its `"deleted"` field to `true` ([raw cast](./deletion.cast)):
![Repo deletion demo](./deletion.svg)

