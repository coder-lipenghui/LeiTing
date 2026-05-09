# Bullet Sprite Guide

Put bullet and laser sprites in this folder:

```text
Assets/Art/Sprites/Bullets/
```

Current configured files:

```text
player_bullet_01.png
enemy_bullet_01.png
player_laser_01.png
```

Recommended import settings:

```text
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Pixels Per Unit: 100
Alpha Is Transparency: Enabled
Mip Maps: Disabled
Filter Mode: Point for pixel art, Bilinear for painted/glow art
Compression: None or Low Quality for small glowing bullets
Pivot: Center
```

Recommended source sizes:

```text
Normal bullet: 16x32 px or 24x48 px, transparent PNG
Round enemy bullet: 24x24 px or 32x32 px, transparent PNG
Piercing bullet: 16x48 px or 24x64 px, transparent PNG
Laser beam: 32x256 px or 48x512 px, vertical beam, transparent PNG
```

Art direction notes:

```text
Player bullets should point upward in the source image.
Enemy bullets can be round, or point upward if they need rotation later.
Laser sprites should be vertical, with the beam centered horizontally.
Keep empty transparent padding tight around the visible shape.
Use bright center pixels and softer outer glow for readability.
```

Runtime sizing comes from the Luban tables, not the source PNG size. Tune `size`, `laserLength`, `muzzleSpacing`, `spreadAngle`, and `pierceCount` in the generated config source tables.
