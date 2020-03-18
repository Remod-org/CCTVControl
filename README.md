# CCTVControl
Current version 1.0.0 (very much a work in progress as of March 2020)

Uses Friends, Clans, Rust Teams

### Overview
Provides basic CCTV control, currently only for finding cameras.

This plugin strives to provide remote camera control.  For now, it can locate your cameras and add them to your computer station.

For normal players, it will find their cameras within a range of 200m by default.

If support for Friends, Clans, or Rust Teams is enabled, it will find their friend's cameras as well.

For admins, or those with a specific permission, it will locate all cameras on the map (default 4000m).  It does not add these cameras and is merely for reference.

### Permission

- `cctvcontrol.use` = Allows use of the /cctv command to add cameras to a computer station.
- `cctvcontrol.admin` = Allows admin list of ALL cameras, regardless of owner.

### Configuration

```json
{
  "userRange": 200.0,
  "adminRange": 4000.0,
  "userMapWide": false,
  "useFriends": true,
  "useClans": false,
  "useTeams": false
}
```

Note that you can set the default search range for users and admins.

You can also selectively enable support for Friends, Clans, and Rust Teams.

If you wish, you can also enable a map-wide search for users to add their cameras.  This will cause the plugin to use the value for adminRange when the user is searching.  They will still be limited to adding cameras they or their friends own.

