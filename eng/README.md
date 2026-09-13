# Engineering scripts

## PowerShell

```powershell
./eng/build.ps1
```

Optional parameters:

```powershell
./eng/build.ps1 -Configuration Debug -SkipFormat -SkipTests
```

## Bash

```bash
./eng/build.sh
./eng/build.sh Debug
```

Both scripts run the same core quality gates as the GitHub Actions CI workflow: restore, formatting verification, build, and tests.
