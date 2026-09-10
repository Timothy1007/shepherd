# Complete local Git history backup

This repository was migrated from an existing local project. The original local Git object history, including all original commit hashes through `83fe9e4` and the latest market-background checkpoint, is preserved in the six bundle parts in this directory.

To restore the original repository on a computer:

```bash
cat shepherd-history.bundle.part* > shepherd-history.bundle
git clone shepherd-history.bundle shepherd-card-table
```

The visible GitHub commit history preserves the original commit messages in migration order. The bundle is the authoritative byte-for-byte Git history backup.
