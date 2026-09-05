# Review checklist

Use this as a decision aid, not a scorecard. Skip irrelevant sections and avoid inventing findings.

## Product and process

- Is the interface organized around the feature and user task rather than an arbitrary shell?
- Does it expose only functionality that is intended to exist now?
- Could a smaller useful version reduce complexity without weakening the task?
- Are low-level choices constrained by reusable systems rather than one-off values?

## Hierarchy and actions

- Can a user identify primary, secondary, and tertiary information at a glance?
- Are secondary elements de-emphasized before primary elements are made louder?
- Do size, weight, contrast, color, and surface area work together?
- Are labels omitted when the value is self-explanatory, combined with values when helpful, and styled according to scanning needs?
- Is the platform's semantic and accessibility structure preserved independently from visual emphasis?
- Is there usually one obvious primary action, with secondary and tertiary actions quieter?
- Are destructive actions emphasized at the confirmation point rather than everywhere they appear?

## Layout and spacing

- Is there enough breathing room, with deliberate exceptions for genuinely dense tools?
- Do spacing values follow a consistent scale that suits the content? A linear scale is acceptable when it works; change it only to address a concrete problem.
- Does each region use only the width its content needs?
- Are fixed, fluid, and maximum widths chosen from content needs rather than grid dogma?
- Does layout adapt to smaller windows while respecting user-selected text and display scaling, without shrinking text or controls to cancel that scaling?
- Is spacing inside a group smaller than spacing between groups?
- Does the layout remain clear at narrow and wide viewports?

## Typography

- Does the interface use a constrained, practical type scale?
- Are small UI text weights legible?
- Are paragraphs generally kept to readable line lengths?
- Are mixed font sizes aligned by baseline when appropriate?
- Does line height account for both font size and line length?
- Are links emphasized according to importance and context?
- Is long text aligned for its writing direction, with numbers aligned for comparison?
- Is letter spacing changed only for a clear reason, such as headlines or all-caps labels?

## Color and accessibility

- Are neutral, primary, accent, and semantic colors represented by deliberate shade scales?
- Do lighter and darker shades retain their intended character rather than being generated blindly?
- Do neutrals have a consistent temperature when tinted?
- Does text and essential UI meet the applicable current accessibility requirements?
- Is meaning available without color alone?
- Are colored backgrounds and foregrounds balanced without washed-out opacity tricks?

## Depth

- Where lighting cues are used, is their implied direction consistent?
- Where shadows communicate layering, do they follow a coherent elevation scale?
- Where interaction changes elevation, does that change clarify the state? Do not require elevation changes in an established flat design.
- Would background color, solid offsets, or overlap communicate depth more simply?
- Do overlapping images retain visible separation?

## Images

- Are photos sufficiently strong for their visual prominence?
- Is text contrast consistent across variable imagery?
- Are icons, screenshots, and logos displayed near the sizes for which their detail was designed?
- Does cropping suit the image's role and existing product behavior? Keep thumbnail or decorative crops distinct from the primary viewer's fit, zoom, and pan behavior.
- Is background bleed prevented without adding noisy borders?

## Finishing

- Does each empty state explain its cause and offer an appropriate next action? Distinguish first use, no results, loading, and failure; preserve controls that restore content or support recovery.
- Can existing bullets, links, quotes, or controls carry visual character before new decoration is added?
- Can borders be replaced by spacing, background changes, or subtle shadows?
- Are accent borders, patterns, gradients, shapes, and illustrations restrained enough to protect readability?
- Could a conventional menu, table, or choice control communicate better by grouping related content differently?

## Evidence boundaries

- Verify mutable external standards, including accessibility criteria, at execution time.
- Do not infer responsive, interaction, or assistive-technology success from a static screenshot.
- Do not infer visual quality from successful compilation or tests.
