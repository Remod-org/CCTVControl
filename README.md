# CCTVControl
Current version 1.0.7 [Download](https://code.remod.org/CCTVControl.cs)

Uses Friends, Clans, Rust Teams

### Overview
Provides basic CCTV control, including pan/tilt, and adding and removing cameras to a station.

For normal players, it will find their cameras within a range of 200m by default.

If support for Friends, Clans, or Rust Teams is enabled, it will find their friend's cameras as well.

For admins, or those with a specific permission, it will locate all cameras on the map (default 4000m).  It does not add these cameras and is merely for reference.

### Commands

- `/cctv` - When in range (2m) of a Computer Station, will add any local cameras in range (200m) of the station that the user owns
- `/cctv add` - When in range (2m) of a Computer Station, will add cameras to the station's list.  This can be a single name or a comma-separated list.  For example:

  /cctv add MYFANCYCAM1, MYOTHERCAM1,COBALT1
  /cctv add MYFANCYCAM2

- `/cctv clear` - When in range (2m) of a Computer Station, will clear that station's list.
- `/cctvlist` - Admin command to list ALL cameras

Note that /cctv commands can also be run from F1 console while mounted to a station, the advantage being that you can up-arrow to easily repeat commands.

When you run /cctvlist from RCON, the plugin will do it's scan from the center of the map and should (now) display the list correctly.

NOTE: When controlling camaras remotely, use the WASD keys for pan and tilt.

Players with the cctvcontrol.admin permission can also pan and tilt server static cameras.

### Permission

- `cctvcontrol.use` = Allows use of the /cctv command to add cameras to a computer station.
- `cctvcontrol.admin` = Allows /cctv user to add all cameras, regardless of owner, and to move static cameras.
- `cctvcontrol.list` = Allows admin list of ALL cameras, regardless of owner using /cctvlist.

### Configuration

```json
{
  "userRange": 200.0,
  "adminRange": 4000.0,
  "userMapWide": false,
  "playAtCamera": true,
  "playSound": true,
  "useFriends": false,
  "useClans": false,
  "useTeams": false,
  "blockServerCams": false
}
```

Note that you can set the default search range for users and admins.

You can also selectively enable support for Friends, Clans, and Rust Teams.

If you wish, you can also enable a map-wide search for users to add their cameras.  This will cause the plugin to use the value for adminRange when the user is searching.  They will still be limited to adding cameras they or their friends own.

If you set blockServerCams true, a user without the cctvcontrol.admin permission will not be able to add server/monument cameras in bulk.  There is currently no way to prevent them from adding them once mounted.  But, the plugin will remove them when they remount the station.

If, however, the cctvcontrol.admin permission is set, the user will be able to add all cameras, regardless of owner.

