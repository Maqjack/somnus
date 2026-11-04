# Security policy

## Supported versions

| Version | Supported |
|---|---|
| 1.x | Yes |
| Earlier builds (`*-dev`, release candidates) | No |

## Reporting a vulnerability

Please do not open a public issue for a security problem.

- Use GitHub's private vulnerability reporting: **Security** tab, **Report a vulnerability**.
- If that is not available, contact the maintainer through GitHub ([@Maqjack](https://github.com/Maqjack))
  and ask for a private channel.

Include the Somnus version, your Windows version, what an attacker could do and the steps
to reproduce it. You can expect a first answer within 7 days. Confirmed problems are fixed
in a new release, and the release notes credit the reporter unless they prefer otherwise.

## Security design in short

- Somnus never connects to the network and has no telemetry (project rule R-07). Only its installer goes online, to download the .NET Desktop Runtime from Microsoft when the PC lacks it, and it checks the SHA-256 of that download (ADR 0010).
- It has no accounts, keys or other secrets.
- It runs as a standard user and never asks for administrator rights (R-08). Only Microsoft's .NET installer does, once, on a PC that lacks the runtime.
- It stores only its settings and logs in `%APPDATA%\Somnus`; logs write the profile folder as `%USERPROFILE%`, so they do not contain the user name.
- Releases are not code-signed (ADR 0004). Verify downloads against the SHA256 checksums
  published with each release.
