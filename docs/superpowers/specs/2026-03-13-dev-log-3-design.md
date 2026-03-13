# Moongate V2 Dev Log #3 Design

## Working Title

Moongate V2 Dev Log #3: Player Portal, Maps, Books, and a .NET Bug I Didn't Own

## Goal

Write a weekly roundup article that:

- opens with a short, concrete story about a .NET/runtime bug that consumed two days
- keeps the main emphasis on visible progress shipped during the week
- positions the player portal as the headline feature
- covers the other meaningful areas of progress without turning into raw release notes

## Audience

- developers following Moongate progress
- emulator/server developers interested in architecture and tooling
- technically curious readers who want visible product progress, not only backend internals

## Core Thesis

This week Moongate became more useful to players, easier to observe as a world, and more flexible to author, even while development time was partially lost to a runtime bug outside the project itself.

## Narrative Shape

### Opening Hook

Start with a short story about the macOS/.NET debugger/runtime issue:

- it looked like an application bug at first
- it consumed roughly two days
- the root cause turned out to be outside Moongate
- include the GitHub issue link as supporting evidence, not as the article's center of gravity

Purpose of the hook:

- establish the emotional reality of the week
- create contrast with how much shipped anyway
- reinforce engineering credibility through accurate attribution

### Main Body Structure

The article should read like a curated weekly roundup, not a changelog dump.

#### 1. Player Portal

This is the main section and should receive the most space.

Topics to cover:

- authenticated player account portal
- profile navigation
- password change flow
- inventory view
- bank view
- branding, localization, and dark fantasy styling

Primary message:

Moongate now has a meaningful player-facing surface, not only server-side progress.

Assets:

- three screenshots in this section
- preferred screenshot subjects:
  - portal landing or profile view
  - inventory view
  - bank view

#### 2. Maps and World Visibility

Topics to cover:

- maps route and sidebar entry
- zoom/pan map viewer
- UO coordinate crosshair on hover
- online player markers

Primary message:

The world is becoming easier to inspect and reason about, which helps both operators and development.

#### 3. Books, Templates, and Content Authoring

Topics to cover:

- readonly books
- writable book flow
- fixes around tooltips and page requests
- text template rendering for gumps

Primary message:

Content is becoming easier to author and less dependent on hardcoded behavior.

#### 4. Mobile State and Protocol Progress

Topics to cover:

- typed mobile state
- effective modifiers
- persisted skills
- player status packet modernization
- packet coverage documentation progress

Primary message:

Visible features were supported by real model and protocol work, not only UI polish.

#### 5. Polish and Runtime Work

Keep this section short and selective.

Topics to cover:

- linked door fixes
- persisted door facing metadata
- pooled buffers and reference-list refactor on hot paths

Primary message:

A meaningful part of the week was still spent reducing friction and hardening engine behavior.

### Closing

End with a short synthesis:

- player-facing features are becoming tangible
- tooling for understanding the world is improving
- the content pipeline is getting more flexible
- progress still depends on surviving platform and runtime surprises

## Tone

- practical and personal
- confident but not over-polished
- technical enough to be credible
- less postmortem-heavy than Dev Log #2
- avoid sounding like release notes

## What To De-Emphasize

- do not let the .NET issue dominate the article
- do not enumerate every commit from the week
- do not over-explain internal refactors unless they directly support the narrative

## Source Material From Recent Commits

Relevant themes from the last week:

- portal: account portal, profile navigation, password change, inventory and bank pages, branding/localization, dark-only fantasy theme
- maps: map page, zoom/pan viewer, coordinate crosshair, online player markers
- books/content: readonly and writable books, book tooltip and request fixes, text-template rendering for gumps
- model/protocol: typed mobile state, effective modifiers, persisted skills, player status packet modernization, packet coverage docs
- engine/polish: linked door behavior, door facing persistence, pooled buffers on hot paths

## Writing Constraints

- target a readable blog-post length, not a long-form technical essay
- use section transitions so the piece reads as a narrative roundup
- keep paragraphs relatively short
- anchor claims in shipped work, not promises

## Deliverable After This Spec

Produce an article outline with:

- final title
- subtitle options
- intro draft
- section-by-section bullet points
- suggested screenshot placement
