# 3D Grid-Based Building Tool

An editor-oriented 3D grid building system for rapid indoor level prototyping.

---

## Setup

1. Create an empty `GameObject` and attach the **`BuildingTool`** component.
2. In the Inspector click **"Generate Default Primitives"** — this creates  
   `Assets/BuildingTool/Generated/` with Floor, Wall, Corner and Stair prefabs  
   and a `DefaultPalette.asset` already wired up.
3. *Optionally* adjust **Grid Size** (default 1 unit).

---

## Controls (Scene View)

| Key / Button       | Action                              |
|--------------------|-------------------------------------|
| Left Click         | Place object                        |
| Right Click        | Remove object at cursor             |
| **R**              | Rotate ghost 90°                    |
| **C**              | Cycle object type in palette        |
| **N**              | Floor down (−1 level)               |
| **M**              | Floor up (+1 level)                 |
| **Shift** + Click  | Place + auto-extend wall            |

---

## Alignment System

Each `PlaceableObject` has an **Alignment** property that controls where within  
a grid cell the object snaps. No manual offset tweaking required.

| Alignment | Where it snaps                       | Default use    |
|-----------|--------------------------------------|----------------|
| `Center`  | Exact cell centre (X,Z)              | Floors, Stairs |
| `Edge`    | Mid-point of one cell edge           | Walls          |
| `Corner`  | One of the four cell corners (X & Z) | Corner posts   |

### How rotation interacts with Edge alignment

A wall at **0°** snaps to the **+Z edge** of its cell.  
Rotate it **90°** → it snaps to the **+X edge**.  
Rotate it **180°** → it snaps to the **−Z edge** (i.e. the *same* edge as its  
neighbour's 0° wall → no gap, no overlap).

This is handled entirely by `PlaceableObject.GetAlignmentOffset()` — the editor  
and runtime placement code just call that method; no branch per-type needed.

### Floors & Walls coexist on the same cell

Floors (Center) and Walls (Edge) are tracked in separate data stores, so you  
can place a floor tile *and* a wall on the same grid cell without either  
blocking the other.

---

## Using Custom Prefabs

1. Give your model a root `GameObject` whose pivot sits at the **grid anchor  
   point** (bottom-centre of the piece as it should sit on the floor).
2. Add the `PlaceableObject` script to the root and set:
   - **Object Type** – descriptive name shown in the inspector
   - **Size** – footprint in grid units (1×1×1 for most pieces)
   - **Alignment** – `Center`, `Edge`, or `Corner`
3. Drag the prefab into your `BuildingPalette.availableObjects` list.

---

## Debug Gizmos

Enable **Show Grid** / **Show Alignment Offset** in the `BuildingTool` inspector  
to visualise:
- Yellow dot → active cell centre  
- Cyan dots  → edge midpoints  
- Orange dots → corner positions  
- Cyan line   → alignment offset vector applied to ghost
