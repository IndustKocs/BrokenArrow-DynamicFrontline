# Launcher 0.4.3

GitHub API returned launcher-ui2 before the later full-debug6 release. Both updaters incorrectly selected the first response element. The tester correctly completed installation but received full-debug5.

Both selectors now explicitly sort eligible releases by published_at descending, with 100 entries requested. SHA256/origin checks remain. Version 0.4.3 is above old launcher tags so legacy selectors can discover the migration release; it contains unchanged full-debug6 mod binaries.

Validation: 24 updater checks including shuffled publication order; real PowerShell selector evaluated against the same regression fixture; final ZIP inventory and embedded ZIP verification. No game installation or launch. After publishing, verify API first element for legacy updater compatibility.
