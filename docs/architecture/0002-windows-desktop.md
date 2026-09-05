# ADR 0002: Windows desktop delivery

- Status: Accepted
- Date: 2026-09-04
- Supersedes ADR 0001's primary delivery model; retains its React/API boundaries.

Issue #11 changes the primary product from a locally hosted web app to a Windows application. A WPF shell hosts React in WebView2 and owns an in-process ASP.NET Core host. `BoardHost` shares API composition and database initialization with the existing server entry point. The UI and API contracts are unchanged.

A portable ZIP bundles a self-contained .NET publish, production frontend assets, and a pinned Fixed Version WebView2 runtime. This avoids custom single-executable extraction and does not require runtime installation. Browser updates require a new package. Docker server builds remain available for development and regression testing.

Desktop Kestrel binds only to an OS-assigned IPv4 loopback port, irrespective of server endpoint configuration. Navigation is restricted to that origin; external windows, downloads, permissions, host objects, and web messaging are disabled. Existing API authentication gaps are unchanged; agent authentication and endpoint discovery remain future work.

Desktop storage resolution lives outside the window, preserving the existing SQLite path and connection-string override. Relative paths resolve against the executable directory. Browser state lives beside, and separately from, ticket data. A future project-file story will define opening and saving boards; this issue does not introduce that format.

WPF compilation and packaged-app checks run separately from the Linux solution. Windows CI and desktop smoke tests are approved exceptions to Docker-only validation; host development software is not installed.
