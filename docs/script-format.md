# Script Format for Handy

## Introduction
Scripts are simple text files containing movement information for the Handy device. They are most commonly paired with videos to synchronize the Handy's movements with the video action.

## Formats
Scripts come in multiple formats:

### 1. Funscript (JSON)
- **Format:** JSON
- **Fields:**
  - `actions`: Array of objects, each with:
    - `at`: Timestamp in milliseconds
    - `pos`: Position (0 = bottom, 100 = top)
- **Example:**

```json
{
  "actions": [
    { "at": 0, "pos": 100 },
    { "at": 1000, "pos": 0 },
    { "at": 2000, "pos": 100 },
    { "at": 3000, "pos": 0 },
    { "at": 4000, "pos": 100 }
  ]
}
```

### 2. CSV
- **Format:** Comma Separated Values
- **Fields:**
  - Each line: `timestamp, position`
    - `timestamp`: Milliseconds
    - `position`: 0 (bottom) to 100 (top)
- **Meta:** The first line can be reserved for metadata (author, software, etc.), but can be left blank.
- **Example:**

```

0, 100
1000, 0
2000, 100
3000, 0
4000, 100
```

### 3. Token (CSV)
- **Format:** CSV
- **Purpose:** Does not contain movement data, but references a file path to actual script data on Handy servers (used by ivdb.io downloads).

## How to Start Scripting
- Use [Handy Free Scripter](https://playground.handyfeeling.com/scripter3) (not for commercial use).
- Community tools: [FunScripter](https://funscripter.com/) (Beta), [EroScripts Community](https://discuss.eroscripts.com/), [HandyScripts Reddit](https://www.reddit.com/r/HandyScripts/), [Discord](https://discord.gg/handy).

## Additional Links
- [thehandy.com](https://thehandy.com/)
- [Developer Docs](https://intercom.help/ohdoki/en/collections/5757680-developer)

---
*Extracted and summarized from [Ohdoki Help Center](https://intercom.help/ohdoki/en/articles/8259972-scripts#h_8c0ea56790) on May 28, 2026.*
