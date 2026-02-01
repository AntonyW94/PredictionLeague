# Data Archiving

## Status

**Not Started** | In Progress | Complete

## Summary

Archive old seasons and predictions to maintain database performance.

## Priority

**Low** - Post-launch improvement

## Requirements

- [ ] Define data retention policy
- [ ] Create archive tables/database
- [ ] Implement archiving process
- [ ] Provide read-only access to archived data
- [ ] Schedule automated archiving

## Data to Archive

| Table | Archive After |
|-------|---------------|
| UserPredictions | Season end + 1 year |
| RoundResults | Season end + 1 year |
| LeagueRoundResults | Season end + 1 year |
| Matches | Season end + 2 years |
| Rounds | Season end + 2 years |

## Technical Notes

Options:
- Archive to separate database
- Archive to blob storage (parquet/CSV)
- Partition tables by season

Keep archived data queryable for historical stats features.
