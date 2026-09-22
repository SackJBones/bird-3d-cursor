# Development checkpoint

## Initial pass

- Both repositories cloned; feature/vrchat-modernization pushed in each.
- Initial request preserved; implementation plan and platform gates recorded.
- Fixed OpenXR pose-read compilation/failure handling and stopped-subsystem reuse.
- 36 contract checks pass. These use API doubles, not Unity or physical tracking.
- Git diff whitespace check passed; package branch local HEAD matched its upstream after push.
- Headset detected by ADB but unauthorized. User must accept USB debugging in the headset before deployment/inspection.
- Both demo projects are Unity 2020.3.33f1. Required VRChat editor installation and real compilation remain pending.

## Recurring task

App automation ID: `develop-bird-for-vrchat`. Heartbeat targets this existing conversation. Normal cadence: every two hours, one small development checkpoint per run. Saved approvals include gh repo operations and `git push -u origin feature/vrchat-modernization`. The automation API does not expose a separate permission-profile setting; unattended inheritance must be verified by the actual scheduled run.

For the initial scheduler test, temporarily use a two-minute interval. The first triggered pass MUST restore the normal two-hour cadence through automation_update before doing any work, retaining the existing name, destination, target, status and normal prompt. The normal prompt is stored in `RECURRING-PROMPT.txt`. Then verify repo read/write, execute the contract checks, update this checkpoint with actual scheduled-run evidence, commit and push. Do not claim the scheduler test passed based only on the manual initial pass. If unable to restore cadence, pause the automation and report the failure instead of running development every two minutes.

Current scheduler test status: pending actual app-triggered run. Manual workspace/test/GitHub push preflight succeeded. Computer must remain on with the app running for local scheduled work.
