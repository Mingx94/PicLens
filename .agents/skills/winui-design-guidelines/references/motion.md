# Motion

Source: https://learn.microsoft.com/zh-tw/windows/apps/design/signature-experiences/motion

Source snapshot: Microsoft Learn zh-tw page reviewed on 2026-06-06.

Motion should be fast, direct, contextual, and purposeful.

## Guidelines

- Connect states visually when position or size changes so the user can track continuity.
- Keep shared entry points consistent in direction, timing, and easing.
- Respond to input method, posture, and orientation using platform behavior.
- Use small moments of delight only when they reinforce an action.
- Avoid custom animation when WinUI controls, transitions, connected animations, or animated icons already solve the need.
- Use page transitions to guide page-to-page movement.
- Use connected animations to preserve context between surfaces or focus states.
- Typical durations are short: 83 ms for minimal opacity fades and 167/250/333 ms for many position, scale, or rotation transitions. Match platform resources instead of hand-tuning unless necessary.
