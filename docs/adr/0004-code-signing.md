# 0004. No code signing in 1.0
Date: 2026-10-22 · Status: Accepted

## Context
SWPA 3.10 requires a recorded decision on code signing. An unsigned .exe triggers Windows SmartScreen ("Windows protected your PC") and may be blocked by antivirus. Paid certificates cost money every year; free signing exists for open-source projects (e.g. SignPath Foundation), but its eligibility rules (public repo, project maturity) must be checked at the time of applying.

## Decision
- Version 1.0 ships unsigned.
- README section "Troubleshooting" explains: SmartScreen → "More info" → "Run anyway", how to verify the SHA256 checksum, and the portable/`run.bat` alternatives.
- After publication (stage 7) Mac may apply for free open-source signing; the current terms are checked then and a new ADR supersedes this one.

## Consequences
- Zero cost; slightly worse first-run experience for other people.
- Users verify integrity with SHA256 from the release notes.
