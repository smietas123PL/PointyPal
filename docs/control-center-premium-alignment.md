# Control Center Premium Alignment

This document maps the PointyPal landing page design language to native WPF decisions for the Control Center polish pass. The scope is visual alignment only: no app logic, Worker behavior, pointer behavior, hotkeys, provider logic, or functionality changes.

| Landing Page Element | Current Control Center Issue | WPF Implementation Decision |
| --- | --- | --- |
| Hero section | Dashboard status panel reads as a wide admin card and stretches too far on larger windows. | Use a centered max-width page container, a composed hero card with a status pill, concise headline/copy, CTA row, and a Core Node visual on the right. Keep card proportions intentional rather than full-bleed utility layout. |
| Feature cards | Capability cards have the right labels but feel like generic dashboard tiles. | Use `FeatureCard` styling: dark gradient surface, low-opacity cyan border, 24px radius, icon tile, title, compact status/helper line, and restrained hover tint. |
| How-it-works cards/circles | Setup and Help flows lack the landing page's concise step rhythm. | Translate the circle/step idea into compact step cards and numbered/visual accents where useful, especially Setup. Avoid adding new workflows or changing existing actions. |
| CTA buttons | Buttons still risk looking like WPF utility controls. | Use native WPF button templates: primary cyan-to-violet pill with subtle glow, secondary transparent cyan outline, ghost muted text with cyan hover. Apply to all visible Control Center main-page buttons. |
| Diamond/core node | Dashboard has only a simple placeholder-like diamond. | Add reusable native WPF `CoreNodeVisual` built from gradients, rotated rounded squares, subtle glow, and inner highlight. Use it in Home and Setup hero cards. |
| Dark background | Current shell is dark but still panel-like and flat. | Use `#0F1116` root shell with subtle dark gradients for surfaces. Keep `#1A1D23` as elevated dark surface, not a plain gray slab. |
| Cyan/violet accent usage | Cyan appears too broadly in panels; violet can feel arbitrary. | Use Soft Cyan `#22D3EE` for borders, icon color, primary cues, and glow. Use Violet `#8B5CF6` mostly inside gradients/core node edges, not as a dominant UI color. |
| Sidebar/nav | Sidebar is functional but sparse and default-dashboard-like. | Make it a premium navigation rail with dark gradient, low cyan border, 24px radius, 48-52px items, selected cyan tint/border/glow, muted inactive text, and consistent icon alignment. Preserve Advanced visibility logic. |
| Typography | Headings and labels do not yet match the friendly landing hierarchy. | Use larger friendly page/hero headings, short supporting copy, uppercase small cyan labels, and secondary text at 60-80% opacity. Avoid dense administrative copy in top-level pages. |
| Privacy/setup/help cards | Pages still read as settings forms rather than product surfaces. | Replace GroupBox/form impressions with explainer hero cards, step/tutorial cards, settings rows with descriptions, and compact action rows. Preserve existing names, bindings, and handlers. |
