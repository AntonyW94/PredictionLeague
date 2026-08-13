# Todo

This folder contains all planned work organised by category.

## Categories

| Category | Description |
|----------|-------------|
| [architecture](architecture/) | Infrastructure, CI/CD, monitoring, database |
| [features](features/) | User-facing features and functionality |
| [security](security/) | Security improvements and deferred items |

## Planning aids

- [roadmap.md](roadmap.md) - the ordered view of what is outstanding and what has already shipped. **This is the list of record**; the per-category folders below hold the detail behind a row.
- [decision-effort-tiers.md](decision-effort-tiers.md) - the remaining plans ranked by how much product-owner input each needs (quick wins through to big builds).

## How we work through these

One branch and one pull request per item, each branched from the latest `master` - **plans are not
stacked on one another**, because a stack makes the review of the second change include the first.

The loop for a single item:

1. `git checkout master && git pull`, delete the branch just merged, branch afresh.
2. Read the plan's README before writing anything. It usually records decisions that are not obvious
   from the code.
3. Implement, then verify with `dotnet build ThePredictions.sln -c Release
   /p:TreatWarningsAsErrors=true` and `dotnet test ThePredictions.sln`. Coverage is gated per project
   at 100% line and branch, so run the coverage tool when you have touched a gated project.
4. **Retire the plan in the same PR as the work.** Delete the plan folder, add its row to the
   roadmap's "Already Complete" table, strip it from `decision-effort-tiers.md`, and fix any links
   that dangled as a result. A finished plan left in this folder is a to-do list that lies.
5. If only part of an item shipped, **trim** the plan down to the residual instead of deleting it,
   and say in the plan what was deliberately dropped and why.

Trust the code over any status line in these documents. They have drifted before, and a plan that
claims work is outstanding when it has already shipped is the most expensive kind of stale.

When a plan is retired but something in it outlives the work - a decision, a rule, a constraint that
will shape later choices - that part belongs in an [ADR](../decisions/) or the relevant guide, not in
a plan folder kept alive to hold it. [ADR-0017](../decisions/0017-sql-belongs-to-the-persistence-adapter.md)
is the worked example.

## Priority Guide

- **Critical** - Required for public launch
- **High** - Important for stability and security
- **Medium** - Important for good user experience
- **Low** - Post-launch improvements and roadmap items

## Status Key

Each plan uses this status format:

```
**Not Started** | In Progress | Complete
```

The current status is shown in bold.

## Historical Reference

- [audit-history.md](../security/audit-history.md) - Completed security fixes
- [accepted-risks.md](../security/accepted-risks.md) - Accepted risks and deferred items
