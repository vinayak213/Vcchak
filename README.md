# Run & Gun - 2D Action Platformer

A fast-paced side-scrolling shooter inspired by classic run-and-gun gameplay. Built with Unity for Android (primary) and PC platforms.

## Project Structure

```
Assets/
├── Scripts/
│   ├── Player/           # Player controller, health, combat, animation
│   ├── Weapons/          # Weapon system, bullets, pickups
│   ├── Enemies/          # Enemy types, boss, spawner, enemy data
│   ├── AI/               # State machine, enemy AI behavior
│   ├── Combat/           # Damage system, interfaces
│   ├── Managers/         # Game, Audio, Level, Score managers
│   ├── UI/               # HUD, menus, mobile controls, joystick
│   ├── Level/            # Camera, parallax, hazards, platforms, checkpoints
│   └── Utilities/        # Object pool, input manager, collectibles
├── Prefabs/
│   ├── Player/           # Player prefab
│   ├── Enemies/          # Enemy prefabs
│   ├── Weapons/          # Weapon prefabs
│   ├── Bullets/          # Bullet prefabs
│   ├── Effects/          # Particle effects
│   ├── UI/               # UI prefabs
│   └── Pickups/          # Coin, health, weapon pickups
├── Scenes/               # MainMenu, Level_01_Jungle, Level_02_Industrial, Level_03_MilitaryBase
├── Sprites/              # All sprite assets organized by category
├── Animations/           # Animator controllers and clips
├── Audio/                # Music and SFX
│   ├── Music/
│   └── SFX/
├── Materials/            # Materials and shaders
└── Resources/            # Runtime-loaded assets
```

## Setup Instructions (Step-by-Step)

### 1. Open Project in Unity
- Open Unity Hub
- Click "Open" and select this project folder
- Use Unity **2022.3 LTS** or newer
- Wait for import to complete

### 2. Configure Layers & Tags
The TagManager is pre-configured. Verify in **Edit > Project Settings > Tags and Layers**:

**Layers:**
| Layer # | Name |
|---------|------|
| 8 | Player |
| 9 | Enemy |
| 10 | PlayerBullet |
| 11 | EnemyBullet |
| 12 | Platform |
| 13 | Pickup |
| 14 | Hazard |
| 15 | Background |

**Sorting Layers** (back to front):
Background → Parallax_Far → Parallax_Mid → Parallax_Near → Default → Platform → Pickup → Enemy → Player → Bullet → Effect → Foreground → UI

### 3. Configure Physics Collision Matrix
Go to **Edit > Project Settings > Physics 2D** and set:
- PlayerBullet should **NOT** collide with: Player, PlayerBullet, Pickup
- EnemyBullet should **NOT** collide with: Enemy, EnemyBullet, Pickup
- Pickup should **NOT** collide with: Enemy, EnemyBullet, PlayerBullet
- Player should **NOT** collide with: PlayerBullet

### 4. Create Sprite Assets
Create placeholder sprites for all game objects. You can use Unity's built-in shapes or import your own:

**Player Sprites:**
- `Sprites/Player/player_idle.png` (character standing)
- `Sprites/Player/player_run_1-6.png` (run cycle)
- `Sprites/Player/player_jump.png`
- `Sprites/Player/player_crouch.png`
- `Sprites/Player/player_shoot.png`

**Enemy Sprites:**
- `Sprites/Enemies/ground_soldier.png`
- `Sprites/Enemies/flying_enemy.png`
- `Sprites/Enemies/turret.png`
- `Sprites/Enemies/boss.png`

**Environment:**
- `Sprites/Environment/tileset_jungle.png`
- `Sprites/Environment/tileset_industrial.png`
- `Sprites/Environment/tileset_military.png`
- `Sprites/Environment/parallax_bg_1-3.png`

### 5. Create Animator Controllers

**Player Animator** (`Animations/Player/PlayerAnimator.controller`):
1. Create states: Idle, Run, Jump, Fall, Crouch, Shoot, Death
2. Add parameters:
   - `Speed` (float) - horizontal speed
   - `IsGrounded` (bool)
   - `IsJumping` (bool)
   - `IsFalling` (bool)
   - `IsCrouching` (bool)
   - `IsShooting` (bool, trigger)
   - `IsDead` (bool, trigger)
3. Set transitions between states based on parameters

### 6. Create ScriptableObject Data Assets

**Weapon Data** (Create via `Assets > Create > RunAndGun > WeaponData` or right-click in Project):

| Weapon | Damage | Fire Rate | Bullet Speed | Ammo |
|--------|--------|-----------|--------------|------|
| Rapid Fire | 10 | 0.1s | 20 | Infinite |
| Spread Shot | 8 | 0.3s | 15 | 50 |
| Laser Beam | 25 | 0.05s | 0 (beam) | Overheat |
| Explosive | 40 | 0.8s | 12 | 20 |

**Enemy Data** (Create via `Assets > Create > RunAndGun > EnemyData`):

| Enemy | Health | Speed | Damage | Score |
|-------|--------|-------|--------|-------|
| Ground Soldier | 30 | 3 | 10 | 100 |
| Flying Enemy | 20 | 4 | 15 | 150 |
| Turret | 50 | 0 | 20 | 200 |
| Boss | 500 | 2 | 25 | 5000 |

### 7. Build the Player Prefab

1. Create empty GameObject named "Player"
2. Add components:
   - `SpriteRenderer` (set sprite, sorting layer to "Player")
   - `Rigidbody2D` (Freeze Rotation Z, Collision Detection: Continuous)
   - `BoxCollider2D` (adjust to fit sprite)
   - `PlayerController` script
   - `PlayerHealth` script
   - `PlayerCombat` script
   - `PlayerAnimatorController` script
   - `Animator`
3. Create child "GroundCheck" (empty, positioned at feet)
4. Create child "FirePoint" (empty, positioned at weapon muzzle)
5. Configure PlayerController:
   - Ground Check: assign GroundCheck transform
   - Ground Layer: set to "Platform"
   - Move Speed: 7
   - Jump Force: 14
6. Save as prefab in `Prefabs/Player/`

### 8. Build Enemy Prefabs

For each enemy type:
1. Create GameObject with SpriteRenderer, Rigidbody2D, Collider2D
2. Add the appropriate script (GroundEnemy, FlyingEnemy, TurretEnemy, BossEnemy)
3. Add EnemyAI component
4. Assign EnemyData ScriptableObject
5. Configure patrol points, detection ranges
6. Save to `Prefabs/Enemies/`

### 9. Build Bullet Prefabs

1. Create bullet GameObjects with SpriteRenderer, Rigidbody2D (Kinematic), CircleCollider2D (Trigger)
2. Add `Bullet` script (or `ExplosiveBullet` for explosive weapon)
3. Set appropriate layers (PlayerBullet or EnemyBullet)
4. Add TrailRenderer for visual effect
5. Save to `Prefabs/Bullets/`

### 10. Build UI

**Main Menu Scene:**
1. Create Canvas (Screen Space - Overlay)
2. Add `MainMenuUI` script to Canvas
3. Create buttons: Play, Level Select, Settings, Quit
4. Add animated background

**Gameplay HUD:**
1. Create Canvas with `GameplayUI` script
2. Add health bar (Image with Fill), weapon icon, ammo text, score text
3. Create boss health bar (hidden by default)

**Mobile Controls:**
1. Add `MobileControlsUI` to gameplay Canvas
2. Create virtual joystick area (left side)
3. Add buttons: Jump, Shoot, Crouch, Weapon Switch, Pause
4. Add `VirtualJoystick` script to joystick area

### 11. Build Levels

**Level 1 - Jungle:**
1. Create new scene `Level_01_Jungle`
2. Add Tilemap for ground/platforms
3. Place parallax backgrounds (3 layers)
4. Add enemy spawn triggers
5. Place checkpoints
6. Add hazards (spikes, acid pools)
7. Place level end trigger
8. Add LevelManager, AudioManager, GameManager (or use DontDestroyOnLoad instances)

**Level 2 - Industrial:**
- Add moving platforms, conveyor belts
- Turret enemies
- Steam vents (periodic hazards)

**Level 3 - Military Base:**
- Night lighting effects
- More turrets and ground soldiers
- Boss fight at the end

### 12. Configure Object Pooling

Add `ObjectPool` component to a manager GameObject:
- Pre-configure pools for all bullet types
- Pool enemy prefabs
- Pool particle effects
- Set initial pool sizes (bullets: 20, enemies: 10, effects: 15)

### 13. Audio Setup

1. Place music files in `Audio/Music/`
2. Place SFX in `Audio/SFX/`
3. Configure AudioManager with clip references
4. Recommended SFX: shoot, explosion, jump, coin_collect, enemy_death, player_hurt, boss_music

### 14. Build Settings

Go to **File > Build Settings**:
1. Add scenes in order:
   - `Scenes/MainMenu` (index 0)
   - `Scenes/Level_01_Jungle` (index 1)
   - `Scenes/Level_02_Industrial` (index 2)
   - `Scenes/Level_03_MilitaryBase` (index 3)
2. Switch platform to Android
3. Set IL2CPP scripting backend
4. Set target API level 33+

## Key Architecture Decisions

- **Object Pooling**: All bullets, enemies, and effects use `ObjectPool` for zero-allocation gameplay
- **ScriptableObjects**: Weapon and enemy data are data-driven via ScriptableObjects
- **State Machine**: Enemy AI uses a clean FSM pattern (Patrol → Chase → Attack → Retreat)
- **Event-Driven**: Systems communicate via C# events, reducing coupling
- **Mobile-First**: Touch controls with virtual joystick, optimized for 60fps on mid-range Android
- **Singleton Managers**: GameManager, AudioManager, ScoreManager persist across scenes

## Controls

### Keyboard (PC)
| Key | Action |
|-----|--------|
| A/D or Arrow Keys | Move |
| W/S or Up/Down | Aim Up/Down |
| Space | Jump |
| Left Ctrl | Shoot |
| Q | Switch Weapon |
| C or Left Shift | Crouch |
| Escape | Pause |

### Mobile
- Left side: Virtual joystick (move + aim)
- Right side: Jump, Shoot, Crouch, Weapon Switch buttons
- Top right: Pause button

## Performance Notes

- Target: 60fps on mid-range Android (Snapdragon 600 series)
- Object pooling eliminates runtime allocations
- Physics uses 2D only (no 3D overhead)
- Sprite batching via sorting layers
- Camera culling prevents off-screen updates
- Enemy AI uses distance checks before expensive raycasts
