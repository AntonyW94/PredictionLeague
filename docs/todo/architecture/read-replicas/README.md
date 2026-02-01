# Read Replicas

## Status

**Not Started** | In Progress | Complete

## Summary

Implement read replicas for database scaling if needed.

## Priority

**Low** - Post-launch, depends on traffic levels

## Requirements

- [ ] Evaluate if read replicas are needed based on traffic
- [ ] Check Fasthosts support for read replicas
- [ ] Configure connection routing (writes to primary, reads to replica)
- [ ] Handle replication lag considerations

## Technical Notes

This is hosting-dependent. Fasthosts shared hosting may not support read replicas.

Alternatives if replicas not available:
- Aggressive caching
- Query optimisation
- Upgrade to dedicated hosting
- Consider Azure SQL

## When to Consider

- High read-to-write ratio
- Response times degrading
- Database CPU consistently high
