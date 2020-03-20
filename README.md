# CCTVControl
Current version 1.0.3 [Download](https://code.remod.org/CCTVControl.cs)

Uses Friends, Clans, Rust Teams

### Overview
Provides basic CCTV control, currently only for finding cameras.

This plugin strives to provide remote camera control.  For now, it can locate your cameras and add them to your computer station.

For normal players, it will find their cameras within a range of 200m by default.

If support for Friends, Clans, or Rust Teams is enabled, it will find their friend's cameras as well.

For admins, or those with a specific permission, it will locate all cameras on the map (default 4000m).  It does not add these cameras and is merely for reference.

### Commands

- `/cctv` - When in range (2m) of a Computer Station, will add any local cameras in range (200m) of the station that the user owns
- `/cctv clear` - When in range (2m) of a Computer Station, will clear that station's list.
- `/cctvlist` - Admin command to list ALL cameras

Note that /cctv commands can also be run from F1 console while mounted to a station.

When you run /cctvlist from RCON, the plugin will do it's scan from the center of the map and should (now) display the list correctly.

### Permission

- `cctvcontrol.use` = Allows use of the /cctv command to add cameras to a computer station.
- `cctvcontrol.admin` = Allows /cctv user to add all cameras, regardless of owner.
- `cctvcontrol.list` = Allows admin list of ALL cameras, regardless of owner using /cctvlist.

### Configuration

```json
{
  "userRange": 200.0,
  "adminRange": 4000.0,
  "userMapWide": false,
  "useFriends": false,
  "useClans": false,
  "useTeams": false
}
```

Note that you can set the default search range for users and admins.

You can also selectively enable support for Friends, Clans, and Rust Teams.

If you wish, you can also enable a map-wide search for users to add their cameras.  This will cause the plugin to use the value for adminRange when the user is searching.  They will still be limited to adding cameras they or their friends own.

If, however, the cctvcontrol.admin permission is set, the user will be able to add all cameras, regardless of owner.
