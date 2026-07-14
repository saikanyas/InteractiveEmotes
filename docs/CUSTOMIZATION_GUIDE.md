## JSON Customization

This guide provides a comprehensive, step-by-step tutorial on how to customize every aspect of character reactions using the mod's JSON files. This guide is designed for everyone, from beginners to experienced users.

### **Getting Started: Tools and File Locations**

Before you begin, it's highly recommended to use a code editor like **Visual Studio Code (VS Code)**. It's free and will provide syntax highlighting, error checking, and code formatting, which makes editing JSON files much easier.

You can find the customization files in your game's directory:
* `Mods/InteractiveEmotes/assets/emotes/` (This folder contains individual `.json` files for each emote)
* `Mods/InteractiveEmotes/i18n/default.json` (for English dialogue)

---

## **1. Core Concepts: Emotes, Rules, and Actions**

The entire system is built on three core concepts:

* **Emotes:** Each emote has its own dedicated file inside the `assets/emotes/` folder (e.g., `heart.json`). The name of the file is the trigger. You can create rules for any of the game's standard emotes by creating a file with that emote's name.
* **Rules:** Inside each file is a list of rules (`"Reactions": []` and `"ComboReactions": []`). The mod checks these rules **from top to bottom** and uses the **first rule that matches** the current situation. This is the most important principle to remember.
* **Actions:** Every rule must have an `Action` that tells the character what to do (show an emote, display text, or both).

---

## **2. Your First Custom Reaction (Step-by-Step)**

Let's create a set of reactions for the **`hi`** emote. We will create or edit the file `assets/emotes/hi.json`.

### **Step 2.1: The Simplest Rule (A Catch-All)**
A "catch-all" rule has no conditions and will trigger if no other specific rule matches. It's great as a default fallback.

**In `hi.json`:**
```json
{
  "Reactions": [
    {
      "Action": {
        "Emote": "happy"
      }
    }
  ]
}
```
* **What this does:** When you use the "hi" emote, any character will respond with the "happy" emote.

### **Step 2.2: Adding Dialogue**
Let's make them say something. We do this by adding a `DisplayText` key. The value is a "translation key" that links to your dialogue file.

**In `hi.json`:**
```json
{
  "Reactions": [
    {
      "Action": {
        "Emote": "happy",
        "DisplayText": "greeting.generic"
      }
    }
  ]
}
```

**In `i18n/default.json`:**
You must add the corresponding key and the text you want to show.
```json
{
  "greeting.generic": "Hello there!"
}
```
* **What this does:** Now, when a character reacts, they will show the "happy" emote and a text bubble that says "Hello there!".

### **Step 2.3: Adding Randomness**
To make interactions feel less repetitive, you can provide a list of options for `Emote` and `DisplayText`. The mod will pick one at random each time.

**In `hi.json`:**
```json
{
  "Reactions": [
    {
      "Action": {
        "Emote": ["happy", "blush"],
        "DisplayText": ["greeting.generic.1", "greeting.generic.2"]
      }
    }
  ]
}
```

**In `i18n/default.json`:**
```json
{
  "greeting.generic.1": "Hey, good to see you!",
  "greeting.generic.2": "Hi there, @!"
}
```
* **What this does:** Now, the character might show a "happy" emote and say "Hey, good to see you!", or they might show a "blush" emote and say "Hi there, [Your Name]!".

---

## **3. Mastering `Conditions`**

The `Conditions` object allows you to create highly specific rules. A rule will only trigger if **ALL** conditions inside its `Conditions` block are true.

### **By Specific Name**
* **Key:** `Name`
* **Type:** `string`
* **Description:** The rule only applies to the character with this exact name.
* **Example:**
    ```json
    {
      "Conditions": {
        "Name": "Abigail"
      },
      "Action": { "Emote": "game" }
    }
    ```

### **By Relationship Status**
* **Key:** `IsSpouse`
* **Type:** `boolean` (`true` or `false`)
* **Description:** Checks if the character is your spouse.
* **Example:**
    ```json
    {
      "Conditions": {
        "IsSpouse": true
      },
      "Action": { "Emote": "heart" }
    }
    ```

* **Key:** `IsDateable`
* **Type:** `boolean` (`true` or `false`)
* **Description:** Checks if the character is one of the bachelors or bachelorettes.
* **Example (for a dateable NPC who is NOT your spouse):**
    ```json
    {
      "Conditions": {
        "IsDateable": true,
        "IsSpouse": false
      },
      "Action": { "Emote": "blush" }
    }
    ```

### **By Friendship Level**
* **Key:** `FriendshipGreaterThanOrEqualTo`
* **Type:** `integer`
* **Description:** Triggers if friendship points are at or above this value (1 heart = 250 points).
* **Example (for 8+ hearts):**
    ```json
    {
      "Conditions": {
        "FriendshipGreaterThanOrEqualTo": 2000
      },
      "Action": { "Emote": "happy" }
    }
    ```

* **Key:** `FriendshipLessThan`
* **Type:** `integer`
* **Description:** Triggers if friendship points are below this value.
* **Example (for less than 2 hearts):**
    ```json
    {
      "Conditions": {
        "FriendshipLessThan": 500
      },
      "Action": { "Emote": "question" }
    }
    ```

### **By Character Type**
* **Key:** `CharacterType`
* **Type:** `string` or `array of strings`
* **Description:** Checks the general type of the character. Valid types are `"Villager"`, `"Pet"`, `"FarmAnimal"`, and `"Baby"`.
* **Example (for any animal):**
    ```json
    {
      "Conditions": {
        "CharacterType": ["Pet", "FarmAnimal"]
      },
      "Action": { "Emote": "happy" }
    }
    ```

### **By Pet Type**
* **Key:** `PetType`
* **Type:** `string`
* **Description:** A more specific check for pets. Valid types are `"Dog"`, `"Cat"`, `"Horse"`, and `"Turtle"` (for compatibility with some mods).
* **Example:**
    ```json
    {
      "Conditions": {
        "PetType": "Dog"
      },
      "Action": { "DisplayText": "animal.dog.happy1" }
    }
    ```

### **By World State (Season & Weather)**
* **Key:** `Season`, `Weather`
* **Type:** `string`
* **Description:** Checks the current season or weather.
    * **Seasons:** `"spring"`, `"summer"`, `"fall"`, `"winter"`
    * **Weather:** `"Sunny"`, `"Rainy"`, `"Windy"`, `"Stormy"`, `"Snowy"`
* **Example (a special greeting on a rainy Fall day):**
    ```json
    {
      "Conditions": {
        "Season": "fall",
        "Weather": "Rainy"
      },
      "Action": { "DisplayText": "greeting.rainy_fall" }
    }
    ```

---

## **4. The Rule Priority Principle (Very Important!)**

The mod checks rules from top to bottom. This means you **must** place your most **specific** rules **above** your more **general** rules.

#### **Incorrect Order ❌**
This will not work as intended. The first rule (the general one) will always be chosen, and the specific rule for friends will never be reached.
**In `heart.json`:**
```json
{
  "Reactions": [
    {
      "Action": { "Emote": "question" } // General rule is first
    },
    {
      "Conditions": { "FriendshipGreaterThanOrEqualTo": 2000 }, // Specific rule is second
      "Action": { "Emote": "heart" }
    }
  ]
}
```

#### **Correct Order ✅**
The specific rule for high friendship is placed first. If that condition isn't met, the mod will then fall back to the general rule below it.
**In `heart.json`:**
```json
{
  "Reactions": [
    {
      "Conditions": { "FriendshipGreaterThanOrEqualTo": 2000 }, // Specific rule is first
      "Action": { "Emote": "heart" }
    },
    {
      "Action": { "Emote": "question" } // General rule is the fallback
    }
  ]
}
```

---

## **5. Understanding Combos (`ComboReactions`)**

The `ComboReactions` list works exactly like `Reactions`, but is placed alongside it in the same file. It requires one extra key:

* **`TriggerCount`**: An `integer` that specifies how many times you must perform the emote in a row to trigger the combo reaction.

**Example Combo Rule (in `happy.json`):**
```json
{
  "ComboReactions": [
    {
      "Conditions": { "FriendshipGreaterThanOrEqualTo": 1000 },
      "TriggerCount": 3,
      "Action": {
        "Emote": "blush",
        "DisplayText": "combo.happy.friend"
      }
    }
  ]
}
```
* **What this does:** If your friendship with an NPC is 4 hearts or more, and you use the "happy" emote 3 times in a row, they will respond with a "blush" emote and special dialogue.

---

## **6. Special Features & Syntax**

### **Special Animations (`anim_`)**
For more expressive reactions, you can trigger full-body animations instead of just an emote bubble.
* **How:** In the `Emote` property of an `Action`, add the prefix `anim_`.
* **Available Animations:** `"anim_laugh"`, `"anim_game"`, `"anim_exclamation"`, `"anim_sick"`
* **Example:**
    ```json
    {
      "Conditions": { "Name": "Abigail" },
      "Action": { "Emote": "anim_game" }
    }
    ```

### **Multi-Part Dialogue (`|`)**
You can make NPCs say longer things by splitting dialogue into multiple bubbles.
* **How:** In your `i18n/default.json` file, place a `|` character where you want the split to occur.
* **Example in `i18n/default.json`:**
    ```json
    "greeting.long": "Oh, hello there, @!|It's a beautiful day for farming, isn't it?"
    ```
* **In-Game Result:** The NPC will first show a bubble with "Oh, hello there, [Your Name]!", pause for a moment, and then show a new bubble with "It's a beautiful day for farming, isn't it?".<br><br><br>