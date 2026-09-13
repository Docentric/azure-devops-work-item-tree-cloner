## Summary

Describe the change and why it is needed.

## Validation

- [ ] `./eng/build.ps1` or `./eng/build.sh` passes.
- [ ] Tests were added or updated for behavior changes.
- [ ] No PATs, Authorization headers, or other secrets are included.
- [ ] Documentation was updated when CLI behavior or cloning semantics changed.

## Azure DevOps behavior

- [ ] Parent/Child semantics remain correct.
- [ ] Field-copy behavior was reviewed for server-managed/read-only fields.
- [ ] Any live manual validation used a non-production/test work item tree.
