# Security policy

Report vulnerabilities privately through GitHub security advisories. Do not publish exploits, secrets, personal data, or vulnerable profile artifacts in an issue.

The application must not embed privileged database, GitHub, or release credentials. Local SQLite files and backups are user data and must never be committed. Dependencies are reviewed through Dependabot; CI uses least-privilege permissions.
