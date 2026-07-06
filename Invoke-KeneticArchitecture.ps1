#Requires -Version 7.0
<#
===============================================================================
 KENETIC Trading Intelligence - Repository Architecture Script
===============================================================================
 PURPOSE
   One-time (but safely re-runnable) migration that reorganizes docs/ into the
   permanent eight-system institutional research structure:

     docs/01_Project_Governance   - vision, principles, rules, AI instructions
     docs/02_Trading_Strategy     - strategy, playbooks, workflow, checklists
     docs/03_Research             - research board, hypothesis ledger, frameworks
     docs/04_Digital_Twin         - the primary artifact and its components
     docs/05_Knowledge            - assimilated knowledge and reference material
     docs/06_Data                 - data dictionary, features, context variables
     docs/07_Development          - architecture specs and engineering standards
     docs/08_Daily_Operations     - daily/weekly/monthly reviews and findings
     docs/99_Archive              - superseded material (nothing is deleted)

 GUARANTEES
   - Idempotent: safe to run any number of times.
   - Never overwrites an existing file. Ever.
   - Never deletes. Conflicts and superseded material are ARCHIVED with a
     timestamp under docs/99_Archive, preserving content.
   - Code, data, and tooling folders (src/, database/, exports/, research/,
     tools/, tests/, scripts/, logs/, _backups/, assets/, screenshots/) are
     never touched.
   - Unrecognized files in legacy folders are moved conservatively into the
     mapped destination folder and reported at the end for human review.

 USAGE
   pwsh ./Invoke-KeneticArchitecture.ps1                 # run at repo root
   pwsh ./Invoke-KeneticArchitecture.ps1 -Root C:\Kenetic
   pwsh ./Invoke-KeneticArchitecture.ps1 -DryRun         # preview only
===============================================================================
#>
[CmdletBinding()]
param(
    # Repository root. Defaults to the folder containing this script.
    [string]$Root = $PSScriptRoot,

    # Preview mode: prints every action without changing anything.
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$Root = (Resolve-Path -LiteralPath $Root).Path

$Docs      = Join-Path $Root 'docs'
$Archive   = Join-Path $Docs '99_Archive'
$Stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
$Today     = Get-Date -Format 'yyyy-MM-dd'
$Utf8      = New-Object System.Text.UTF8Encoding($false)   # UTF-8, no BOM

# Running tallies for the final report
$script:Created   = [System.Collections.Generic.List[string]]::new()
$script:Moved     = [System.Collections.Generic.List[string]]::new()
$script:Archived  = [System.Collections.Generic.List[string]]::new()
$script:Skipped   = [System.Collections.Generic.List[string]]::new()
$script:Unmapped  = [System.Collections.Generic.List[string]]::new()

# -----------------------------------------------------------------------------
#  Logging helpers
# -----------------------------------------------------------------------------
function Write-Step  { param([string]$Msg) Write-Host "==> $Msg" -ForegroundColor Cyan }
function Write-Act   { param([string]$Msg) Write-Host "    $Msg" -ForegroundColor Green }
function Write-Note  { param([string]$Msg) Write-Host "    $Msg" -ForegroundColor Yellow }

# -----------------------------------------------------------------------------
#  Core safe primitives - every mutation in this script goes through these
# -----------------------------------------------------------------------------

function Ensure-Folder {
    <# Creates a folder if missing. Idempotent. #>
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) { return }
    if ($DryRun) { Write-Act "[dry-run] mkdir  $Path" }
    else {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Write-Act "created folder  $($Path.Substring($Root.Length + 1))"
    }
    $script:Created.Add($Path)
}

function Write-Utf8File {
    <# Writes a new UTF-8 file ONLY if it does not already exist. #>
    param([string]$Path, [string]$Content)
    if (Test-Path -LiteralPath $Path) {
        $script:Skipped.Add($Path)
        return
    }
    Ensure-Folder (Split-Path -Parent $Path)
    if ($DryRun) { Write-Act "[dry-run] create $Path" }
    else {
        [System.IO.File]::WriteAllText($Path, $Content, $Utf8)
        Write-Act "created file    $($Path.Substring($Root.Length + 1))"
    }
    $script:Created.Add($Path)
}

function Archive-Item {
    <# Moves an item into docs/99_Archive with a timestamp prefix. Never deletes. #>
    param([string]$Path, [string]$Reason)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    Ensure-Folder $Archive
    $leaf   = Split-Path -Leaf $Path
    $target = Join-Path $Archive "$Stamp`_$leaf"
    # Guard against the (unlikely) case the archive slot is taken
    $i = 1
    while (Test-Path -LiteralPath $target) {
        $target = Join-Path $Archive "$Stamp`_$i`_$leaf"; $i++
    }
    if ($DryRun) { Write-Act "[dry-run] archive $Path -> $target  ($Reason)" }
    else {
        Move-Item -LiteralPath $Path -Destination $target
        Write-Note "archived        $leaf  ($Reason)"
    }
    $script:Archived.Add("$Path -> $target")
}

function Move-Safe {
    <#
      Moves Source to Destination with full safety:
        - Source missing        -> no-op (already migrated on a prior run)
        - Destination occupied  -> Source is ARCHIVED instead (never overwrite)
        - Otherwise             -> normal move
    #>
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path -LiteralPath $Source)) { return }
    if (Test-Path -LiteralPath $Destination) {
        Archive-Item -Path $Source -Reason 'destination already exists'
        return
    }
    Ensure-Folder (Split-Path -Parent $Destination)
    if ($DryRun) { Write-Act "[dry-run] move   $Source -> $Destination" }
    else {
        Move-Item -LiteralPath $Source -Destination $Destination
        Write-Act "moved           $($Source.Substring($Root.Length + 1)) -> $($Destination.Substring($Root.Length + 1))"
    }
    $script:Moved.Add("$Source -> $Destination")
}

function Move-FolderContents {
    <#
      Moves every child of a legacy folder into a destination folder, then
      removes the legacy folder only if it ended up empty. Items whose name
      collides at the destination are archived. Every item moved this way
      (other than ones in the explicit map) is recorded for human review.
    #>
    param([string]$SourceFolder, [string]$DestFolder, [switch]$Report)
    if (-not (Test-Path -LiteralPath $SourceFolder)) { return }
    Ensure-Folder $DestFolder
    foreach ($item in Get-ChildItem -LiteralPath $SourceFolder -Force) {
        $dest = Join-Path $DestFolder $item.Name
        if ($Report) { $script:Unmapped.Add("$($item.FullName) -> $dest") }
        Move-Safe -Source $item.FullName -Destination $dest
    }
    # Remove the legacy folder only when empty (safe + idempotent)
    if (-not $DryRun -and (Test-Path -LiteralPath $SourceFolder) -and
        -not (Get-ChildItem -LiteralPath $SourceFolder -Force)) {
        Remove-Item -LiteralPath $SourceFolder
        Write-Act "removed empty   $($SourceFolder.Substring($Root.Length + 1))"
    }
}

function New-SeedDoc {
    <# Creates a purposeful seed markdown document if it does not exist. #>
    param([string]$Path, [string]$Title, [string]$Purpose)
    $content = @"
# $Title

> Created $Today by the KENETIC architecture script. This is the single
> authoritative home for this concept. Update it in place; never fork it.

## Purpose

$Purpose

## Contents

_To be developed as evidence accumulates._

## Change Log

- ${Today}: Document created.
"@
    Write-Utf8File -Path $Path -Content $content
}

function New-FolderReadme {
    <# Creates the master README for a major folder if it does not exist. #>
    param([string]$Folder, [string]$Title, [string]$Purpose,
          [string]$Contents, [string]$Relationships, [string]$Evolution)
    $content = @"
# $Title

## Purpose

$Purpose

## Contents

$Contents

## Relationships

$Relationships

## How This Folder Should Evolve

$Evolution

---
_Master README created $Today. Keep this file current whenever the folder's
structure or responsibilities change._
"@
    Write-Utf8File -Path (Join-Path $Folder 'README.md') -Content $content
}

# =============================================================================
#  PHASE 0 - Sanity checks
# =============================================================================
Write-Step "KENETIC architecture migration starting (root: $Root)$(if ($DryRun) { '  [DRY RUN]' })"

if (-not (Test-Path -LiteralPath (Join-Path $Root 'README.md'))) {
    Write-Note "README.md not found at root. Confirm -Root points at the repository root."
}
Ensure-Folder $Docs

# =============================================================================
#  PHASE 1 - Create the permanent folder structure
# =============================================================================
Write-Step 'Phase 1: creating permanent folder structure'

$Structure = @(
    '01_Project_Governance',
    '02_Trading_Strategy',
    '02_Trading_Strategy/Playbooks',
    '02_Trading_Strategy/Checklists',
    '03_Research',
    '04_Digital_Twin',
    '05_Knowledge',
    '05_Knowledge/Book_Assimilations',
    '05_Knowledge/Research_Papers',
    '05_Knowledge/Market_Concepts',
    '06_Data',
    '06_Data/Features',
    '07_Development',
    '07_Development/Architecture',
    '07_Development/Development_Standards',
    '08_Daily_Operations',
    '08_Daily_Operations/Daily_Reviews',
    '08_Daily_Operations/Daily_Reviews/2026',
    '08_Daily_Operations/Weekly_Reviews',
    '08_Daily_Operations/Weekly_Reviews/2026',
    '08_Daily_Operations/Monthly_Reviews',
    '08_Daily_Operations/Monthly_Reviews/2026',
    '08_Daily_Operations/Experiment_Reports',
    '08_Daily_Operations/Research_Findings',
    '99_Archive'
)
foreach ($rel in $Structure) { Ensure-Folder (Join-Path $Docs $rel) }

# =============================================================================
#  PHASE 2 - Migrate existing documents (explicit map first)
# =============================================================================
Write-Step 'Phase 2: migrating existing documents'

# --- 2a. Explicit map: ONLY cross-folder splits, i.e. files whose destination
#          differs from where the rest of their legacy folder goes. All original
#          filenames are preserved verbatim: this migration RELOCATES, it never
#          RENAMES. Renaming (e.g. dropping numeric prefixes) is a deliberate,
#          separate follow-up commit that updates inbound links in the same diff.
$MoveMap = [ordered]@{
    # Glossary belongs with Knowledge, not Governance
    'docs/00_Foundation/03_Glossary.md'   = 'docs/05_Knowledge/03_Glossary.md'
    # The Digital Twin gets its own top-level home, apart from Research
    'docs/02_Research/05_Digital_Twin.md' = 'docs/04_Digital_Twin/05_Digital_Twin.md'
}
foreach ($key in $MoveMap.Keys) {
    Move-Safe -Source (Join-Path $Root $key) -Destination (Join-Path $Root $MoveMap[$key])
}

# --- 2b. Archive backup folders that live inside the doc tree
foreach ($bak in Get-ChildItem -LiteralPath $Docs -Recurse -Directory -Force -ErrorAction SilentlyContinue |
         Where-Object { $_.Name -like '_backup*' -and $_.FullName -notlike "*99_Archive*" }) {
    Archive-Item -Path $bak.FullName -Reason 'backup folder relocated out of the doc tree'
}

# --- 2c. Wholesale folder migrations (handles files not in the explicit map;
#          those extras are reported at the end for human review)
Move-FolderContents -SourceFolder (Join-Path $Docs '01_Architecture') `
                    -DestFolder   (Join-Path $Docs '07_Development/Architecture') -Report
Move-FolderContents -SourceFolder (Join-Path $Docs '02_Research') `
                    -DestFolder   (Join-Path $Docs '03_Research') -Report
Move-FolderContents -SourceFolder (Join-Path $Docs '03_Features') `
                    -DestFolder   (Join-Path $Docs '06_Data/Features') -Report
Move-FolderContents -SourceFolder (Join-Path $Docs '04_Development') `
                    -DestFolder   (Join-Path $Docs '07_Development/Development_Standards') -Report
Move-FolderContents -SourceFolder (Join-Path $Docs '05_Research_Findings') `
                    -DestFolder   (Join-Path $Docs '08_Daily_Operations/Research_Findings') -Report
Move-FolderContents -SourceFolder (Join-Path $Docs '00_Foundation') `
                    -DestFolder   (Join-Path $Docs '01_Project_Governance') -Report

# =============================================================================
#  PHASE 3 - Create missing operating documents (never overwrites)
# =============================================================================
Write-Step 'Phase 3: creating missing operating documents'

# --- Governance
New-SeedDoc (Join-Path $Docs '01_Project_Governance/Claude_Instructions.md') 'Claude Instructions' `
    'Canonical, version-controlled copy of the AI project instructions (currently Version 3.0: Senior Research Analyst / Digital Twin mission). The chat project instructions should always mirror this file. Also reconcile here the engineering role defined in CLAUDE.md: Research Analyst is the default mode; Engineer mode applies only when explicitly instructed.'
New-SeedDoc (Join-Path $Docs '01_Project_Governance/Project_Rules.md') 'Project Rules (Pointer)' `
    'Pointer document. The authoritative rules live in /PROJECT_RULES.md at the repository root so tooling and AI assistants find them first. Do not duplicate rules here; link and annotate only.'

# --- Trading Strategy
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Current_Strategy.md') 'Current Strategy' `
    'The single authoritative description of the strategy as traded today: instruments, sessions, setups, risk parameters, and management rules. Version material changes; never overwrite history.'
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Strategy_V2.md') 'Strategy V2' `
    'Design space for the next strategy iteration. Proposals graduate into Current_Strategy.md only after satisfying the validation framework.'
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Trading_Workflow.md') 'Trading Workflow' `
    'The end-to-end daily workflow: premarket preparation, session execution, post-session review, and data import into the research system.'
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Checklists/Premarket_Checklist.md') 'Premarket Checklist' `
    'Checklist executed before every session: macro calendar, higher-timeframe context, key levels, regime assessment, and readiness.'
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Checklists/Trading_Checklist.md') 'Trading Checklist' `
    'In-session decision checklist aligned with the analytical hierarchy (context before signal, structure before order flow, order flow before entry).'
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Checklists/Review_Checklist.md') 'Review Checklist' `
    'Post-session review checklist ensuring every session updates the Digital Twin: what strengthened, what weakened, what was rejected, what was discovered.'
New-SeedDoc (Join-Path $Docs '02_Trading_Strategy/Playbooks/README.md') 'Playbooks' `
    'One file per validated playbook. A playbook exists here only after its underlying hypothesis has reached validated status in the Hypothesis Ledger.'

# --- Research
New-SeedDoc (Join-Path $Docs '03_Research/Research_Board.md') 'Research Board' `
    'The active research surface: what is currently being investigated, by what method, and with what expected evidence. Reviewed and pruned regularly.'
New-SeedDoc (Join-Path $Docs '03_Research/Hypothesis_Ledger.md') 'Hypothesis Ledger' `
    'The permanent register of every hypothesis. Each entry carries: Identifier (H-###), Statement, Confidence, Supporting Evidence, Contradicting Evidence, Missing Evidence, Sample Size, and Required Next Test. Confidence changes only because evidence changes. Retired and rejected hypotheses remain in the ledger, marked as such. GROWTH PATH (declared now so the structure never needs to change): this ledger is the authoritative INDEX. When an entry outgrows the ledger, its full evidence record becomes docs/03_Research/Hypotheses/H-###.md and the ledger row links to it.'
New-SeedDoc (Join-Path $Docs '03_Research/Research_Queue.md') 'Research Queue' `
    'Prioritized backlog of questions worth investigating, each with the data required to answer it.'
New-SeedDoc (Join-Path $Docs '03_Research/Experiment_Log.md') 'Experiment Log' `
    'Append-only INDEX of experiments: one line per experiment with Identifier (EXP-###), hypothesis reference (H-###), date, and verdict. The full write-up of each experiment lives at docs/08_Daily_Operations/Experiment_Reports/EXP-###.md; this log links to it. The Hypothesis Ledger references experiments only by EXP identifier. One record, one home: the report is the record, this log is the index.'

# --- Digital Twin (the primary artifact)
$TwinDocs = [ordered]@{
    'Strengths.md'             = 'Validated strengths of the trader, each backed by evidence level, sample size, and supporting sessions.'
    'Weaknesses.md'            = 'Validated weaknesses, each backed by evidence level, sample size, and supporting sessions.'
    'Behavioral_Tendencies.md' = 'Observed behavioral, execution, and decision tendencies, classified as fact, observation, or hypothesis.'
    'Market_Preferences.md'    = 'Preferred and poor environments: regimes, sessions, volatility states, and auction contexts where performance measurably differs.'
    'Performance_Trends.md'    = 'Longitudinal performance evidence over time; trends are stated only with sample size and confidence attached.'
    'Context_Library.md'       = 'The library of recurring market contexts (Context DNA) used to classify sessions and trades consistently.'
    'Knowledge_Graph.md'       = 'How observations, features, events, contexts, and outcomes interconnect. Complements the platform Knowledge Graph vision in PROJECT_RULES.md.'
    'Open_Questions.md'        = 'What the Digital Twin does not yet know, and what evidence would be required to know it. Feeds the Research Queue.'
}
foreach ($name in $TwinDocs.Keys) {
    New-SeedDoc (Join-Path $Docs "04_Digital_Twin/$name") ($name -replace '\.md$','' -replace '_',' ') $TwinDocs[$name]
}

# --- Knowledge
# Auction Market Theory IS a market concept; it lives inside Market_Concepts.
New-SeedDoc (Join-Path $Docs '05_Knowledge/Market_Concepts/Auction_Market_Theory.md') 'Auction Market Theory' `
    'Authoritative internal reference for auction market theory concepts as used by this project: balance, imbalance, value, excess, and auction failure. Terminology here is binding for all analyses.'
New-SeedDoc (Join-Path $Docs '05_Knowledge/Book_Assimilations/README.md') 'Book Assimilations' `
    'One file per assimilated book: the durable concepts extracted, mapped to project terminology, with page-level references.'

# --- Data
# NOTE (deliberate omissions): no Schemas/ folder is created - the authoritative
# schema spec lives with the Architecture documents (cross-referenced by the
# ADR log); create 06_Data/Schemas/ the day the first generated DDL/ERD artifact
# exists. Likewise, no Recorder/Research_Engine/AI seed docs are created under
# 07_Development - the Architecture documents already own those subsystems, and
# duplicating them would create split authority.
New-SeedDoc (Join-Path $Docs '06_Data/Data_Dictionary.md') 'Data Dictionary' `
    'The single authoritative definition of every recorded and derived field: name, type, owner, version, units, and calculation reference. No analysis may use a field that is not defined here.'
New-SeedDoc (Join-Path $Docs '06_Data/Context_Variables.md') 'Context Variables' `
    'The controlled vocabulary of context variables (regime, session, volatility state, auction state) used to tag sessions and trades consistently. Also known in the project instructions as the Context DNA vocabulary; this file and the Digital Twin Context Library share it.'
New-SeedDoc (Join-Path $Docs '06_Data/Metrics.md') 'Metrics' `
    'Definitions of every performance and research metric. One definition, one owner, one version per metric.'

# =============================================================================
#  PHASE 4 - Master READMEs for every major folder
# =============================================================================
Write-Step 'Phase 4: writing master READMEs'

New-FolderReadme -Folder (Join-Path $Docs '01_Project_Governance') -Title '01 Project Governance' `
    -Purpose 'Defines why the project exists and the rules under which it operates: constitution, vision, operating principles, roadmap, and AI instructions.' `
    -Contents 'Constitution, Project Vision, Core Philosophy (the operating principles), Project Roadmap, Claude Instructions, Project Rules pointer. Filenames retain their original numeric prefixes until the deliberate rename commit.' `
    -Relationships 'Authoritative over every other folder. PROJECT_RULES.md at the repository root remains the binding rules document; this folder governs everything else.' `
    -Evolution 'Changes rarely and deliberately. Every change is versioned and dated. Nothing here is deleted; superseded principles are archived.'

New-FolderReadme -Folder (Join-Path $Docs '02_Trading_Strategy') -Title '02 Trading Strategy' `
    -Purpose 'The authoritative description of how trading is actually conducted: current strategy, its next iteration, playbooks, workflow, and checklists.' `
    -Contents 'Current Strategy, Strategy V2, Trading Workflow, Playbooks/, Checklists/.' `
    -Relationships 'Consumes validated knowledge from 03_Research and 04_Digital_Twin. Nothing enters a playbook without passing the validation framework.' `
    -Evolution 'Playbooks are added only when hypotheses validate. The current strategy is versioned; prior versions are preserved.'

New-FolderReadme -Folder (Join-Path $Docs '03_Research') -Title '03 Research' `
    -Purpose 'The research operating system: what is being investigated, every hypothesis and its evidence state, and the frameworks governing validation.' `
    -Contents 'Research Board, Hypothesis Ledger, Research Queue, Experiment Log, Research Framework, Validation Framework, Evidence Levels, Feature Lifecycle.' `
    -Relationships 'Feeds validated knowledge into 04_Digital_Twin and 02_Trading_Strategy. Draws questions from Digital Twin Open Questions. Research never modifies live execution (Law 5).' `
    -Evolution 'The Hypothesis Ledger is append-only in spirit: confidence changes, entries are never silently removed.'

New-FolderReadme -Folder (Join-Path $Docs '04_Digital_Twin') -Title '04 Digital Twin' `
    -Purpose 'The primary artifact of the entire project: the evidence-based model of the trader''s decision-making. Every imported session must change something in this folder.' `
    -Contents 'Digital Twin (master document), Strengths, Weaknesses, Behavioral Tendencies, Market Preferences, Performance Trends, Context Library, Knowledge Graph, Open Questions.' `
    -Relationships 'Updated by every session review in 08_Daily_Operations. Sources evidence from 03_Research. Open Questions feed the Research Queue.' `
    -Evolution 'Grows only through evidence. Every claim carries its epistemic class (fact / observation / hypothesis / validated), sample size, and confidence.'

New-FolderReadme -Folder (Join-Path $Docs '05_Knowledge') -Title '05 Knowledge' `
    -Purpose 'Assimilated external knowledge translated into project terminology: books, papers, market concepts, auction market theory, and the glossary.' `
    -Contents 'Glossary, Book_Assimilations/ (one file per book), Research_Papers/, Market_Concepts/ (including Auction_Market_Theory.md). Add new subfolders only when a real category of material exists; there is deliberately no general reference junk drawer.' `
    -Relationships 'The Glossary is binding vocabulary for every document in the repository. Concepts here inform, but never substitute for, evidence in 03_Research.' `
    -Evolution 'Each assimilation is one file. Concepts are merged into existing entries rather than duplicated.'

New-FolderReadme -Folder (Join-Path $Docs '06_Data') -Title '06 Data' `
    -Purpose 'Definitions that make data trustworthy: the data dictionary, metrics, context variables, and feature definitions.' `
    -Contents 'Data Dictionary, Metrics, Context Variables, Features/ (per-feature definition documents). The authoritative database schema specification lives with the Architecture documents in 07_Development (it is cross-referenced by the Architecture Decision Log); create a Schemas/ folder here only when the first generated artifact (DDL export, ERD) exists.' `
    -Relationships 'Every analysis in 03_Research and every Digital Twin claim must trace to fields defined here. Feature docs follow the Feature Lifecycle in 03_Research.' `
    -Evolution 'Definitions are versioned, never overwritten. A changed calculation is a new version.'

New-FolderReadme -Folder (Join-Path $Docs '07_Development') -Title '07 Development' `
    -Purpose 'Engineering home: system architecture specifications and development standards for the recorder, research engine, and AI layer.' `
    -Contents 'Architecture/ (system overview, principles, decision log, database schema - the single authority on every subsystem), Development_Standards/ (coding standards, workflow, testing, performance, versioning). Subsystem knowledge lives in the Architecture documents; do not create per-subsystem sibling docs that would split authority.' `
    -Relationships 'Implements the platform that 06_Data and 03_Research depend on. Bound by PROJECT_RULES.md and the Architecture Decision Log.' `
    -Evolution 'ADRs are append-only. Architecture changes require a new ADR; accepted entries are never edited in place.'

New-FolderReadme -Folder (Join-Path $Docs '08_Daily_Operations') -Title '08 Daily Operations' `
    -Purpose 'The operational cadence of the research program: daily, weekly, and monthly reviews, experiment reports, and dated research findings.' `
    -Contents 'Daily_Reviews/YYYY/YYYY-MM-DD.md - ONE file per trading day. The file OPENS with the premarket plan (written before the session) and CLOSES with a Digital Twin Update section (written after), so plan versus outcome is visible in a single document. Weekly_Reviews/YYYY/ and Monthly_Reviews/YYYY/ follow the same year-folder convention. Experiment_Reports/EXP-###.md holds the full record of each experiment; the index lives in 03_Research/Experiment_Log.md. Research_Findings/ holds dated findings.' `
    -Relationships 'Every daily review must change something in 04_Digital_Twin (strengthen, weaken, reject, or discover) and, where relevant, the Hypothesis Ledger in 03_Research. Experiment reports are referenced by EXP identifier from the Experiment Log and Hypothesis Ledger.' `
    -Evolution 'Strictly chronological and append-only. Reviews are never edited after the fact; corrections are new dated entries. New year folders are created each January; the structure itself never changes.'

New-FolderReadme -Folder $Archive -Title '99 Archive' `
    -Purpose 'Preservation area. Nothing in this repository is deleted; superseded, duplicate, or displaced material is moved here with a timestamp prefix.' `
    -Contents 'Timestamped files and folders moved by the architecture script or by later manual reorganizations.' `
    -Relationships 'Read-only in spirit. If archived material becomes relevant again, copy it out; do not edit it in place.' `
    -Evolution 'Grows monotonically. Periodically reviewed, never purged without an explicit, documented decision.'

New-FolderReadme -Folder $Docs -Title 'KENETIC Documentation' `
    -Purpose 'Root of the knowledge system. Eight permanent operating systems, numbered in reading order, plus the archive.' `
    -Contents '01 Governance, 02 Trading Strategy, 03 Research, 04 Digital Twin, 05 Knowledge, 06 Data, 07 Development, 08 Daily Operations, 99 Archive.' `
    -Relationships 'Knowledge flows: Operations (08) generate evidence -> Research (03) validates -> Digital Twin (04) and Strategy (02) absorb. Governance (01) rules all; Data (06) and Development (07) make evidence trustworthy; Knowledge (05) supplies vocabulary.' `
    -Evolution 'Folders are permanent. New material goes into an existing system; a new top-level folder requires a governance decision.'

# =============================================================================
#  PHASE 5 - Final report
# =============================================================================
Write-Step 'Phase 5: final report'
Write-Host ''
Write-Host ('  Folders/files created : {0}' -f $script:Created.Count)
Write-Host ('  Items moved           : {0}' -f $script:Moved.Count)
Write-Host ('  Items archived        : {0}' -f $script:Archived.Count)
Write-Host ('  Existing items kept   : {0}' -f $script:Skipped.Count)
Write-Host ''

if ($script:Archived.Count -gt 0) {
    Write-Note 'Archived (review docs/99_Archive at your convenience):'
    $script:Archived | ForEach-Object { Write-Host "    $_" }
    Write-Host ''
}
if ($script:Unmapped.Count -gt 0) {
    Write-Note 'Files migrated by folder rule rather than explicit mapping - confirm their new homes:'
    $script:Unmapped | Sort-Object -Unique | ForEach-Object { Write-Host "    $_" }
    Write-Host ''
}

Write-Note 'MANUAL FOLLOW-UPS (judgment required; intentionally not automated):'
Write-Host '    1. COMMIT 1 - this migration, in isolation (clean git rename detection):'
Write-Host '       "chore(docs): migrate to eight-system knowledge architecture (relocate only)"'
Write-Host '    2. COMMIT 2 - update hard-coded doc paths in README.md, PROJECT_RULES.md, and CLAUDE.md'
Write-Host '       (e.g. docs/02_Research/03_Evidence_Levels.md -> docs/03_Research/03_Evidence_Levels.md,'
Write-Host '        docs/00_Foundation/... -> docs/01_Project_Governance/..., docs/03_Features/ ->'
Write-Host '        docs/06_Data/Features/, docs/05_Research_Findings/ -> docs/08_Daily_Operations/Research_Findings/).'
Write-Host '    3. COMMIT 3 (optional, deliberate) - drop numeric prefixes from moved filenames,'
Write-Host '       updating every inbound link in the same commit. This script intentionally never renames.'
Write-Host '    4. Write the first real article of docs/01_Project_Governance/00_Constitution.md:'
Write-Host '       "Repository structure changes less frequently than trading strategy." Change velocities:'
Write-Host '       structure ~ years; strategy ~ validated research cycles; Digital Twin ~ every meaningful'
Write-Host '       cycle; Research Board ~ continuously. Reorganizations must show why knowledge cannot'
Write-Host '       live in an existing system.'
Write-Host '    5. Reconcile CLAUDE.md (engineer role) with docs/01_Project_Governance/Claude_Instructions.md'
Write-Host '       (research analyst role): analyst is the default, engineer only when explicitly instructed.'
Write-Host '    6. Paste the Version 3.0 project instructions into docs/01_Project_Governance/Claude_Instructions.md.'
Write-Host ''
Write-Step "Migration complete.$(if ($DryRun) { ' No changes were made (dry run).' })"
