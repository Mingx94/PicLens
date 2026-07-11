# PicLens UI concept prompt

Use case: ui-mockup

Asset type: high-fidelity desktop application interface design reference for implementation in Qt Quick

Primary request: Design a polished, production-ready light-theme interface for PicLens, a local image organizer and viewer for Windows and Linux.

Scene/backdrop: full application window only, straight-on orthographic UI screenshot, 16:10 desktop canvas

Layout: compact 60px top command bar; 252px left folder tree; large white library workspace; thin bottom status bar. Top bar contains PicLens wordmark, sidebar toggle, back/forward, refresh, centered search field, and a cobalt primary button labeled exactly "開啟資料夾". Folder tree header text exactly "資料夾". Library header shows exactly "旅行照片", a muted local path, item-count badge, and controls for "含子資料夾", sort, thumbnail size, grid/list, and more. Gallery shows a tidy responsive grid of realistic travel-photo thumbnails and one folder tile. One image tile is selected with a clear cobalt outline and check indicator.

Style/medium: high-fidelity native desktop UI mockup, modern restrained Fluent-inspired visual language but framework-neutral, crisp production design

Composition/framing: full window with all edges visible, no device frame, clear hierarchy, generous but compact spacing, high information density suitable for repeated file work

Lighting/mood: flat UI lighting, calm, professional, image-first

Color palette: warm-neutral app background #F5F6F8, white surfaces, graphite text, cobalt #4968E8 accent, pale cobalt selection, subtle cool-gray borders

Materials/textures: flat opaque surfaces, subtle 1px borders, very soft elevation only for floating controls; rounded corners 8-12px

Typography: clean Traditional Chinese sans-serif, readable at desktop scale

Text (verbatim): "PicLens", "搜尋目前資料夾", "開啟資料夾", "資料夾", "旅行照片", "含子資料夾", "名稱", "縮圖", "已選取 1 張", "共 128 個項目"

Constraints: preserve a practical resizable desktop app layout; all controls must look implementable in Qt Quick; distinct hover/selection/focus affordances; folder navigation must not compete with image gallery; no dark theme

Avoid: browser chrome, macOS traffic lights, mobile styling, glassmorphism, gradients, oversized cards, decorative illustration, marketing page, illegible microtext, invented side panels, excessive shadows, watermark
