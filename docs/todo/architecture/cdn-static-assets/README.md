# CDN for Static Assets

## Status

**Not Started** | In Progress | Complete

## Summary

Serve static assets (CSS, JS, images) via a CDN for improved performance and reduced server load.

## Priority

**Medium** - All static files currently served from web server

## Requirements

- [ ] Evaluate CDN options (Cloudflare, Azure CDN, etc.)
- [ ] Configure CDN for static assets
- [ ] Update asset URLs to use CDN
- [ ] Configure cache headers for CDN
- [ ] Test cache invalidation process

## Options to Consider

| CDN | Pros | Cons |
|-----|------|------|
| Cloudflare (Free) | Free tier, easy setup | Limited control |
| Azure CDN | Good integration | Cost |
| BunnyCDN | Cheap, fast | Additional vendor |

## Technical Notes

May need to:
- Update `wwwroot` asset paths
- Configure CORS for CDN domain
- Set up cache purging for deployments
