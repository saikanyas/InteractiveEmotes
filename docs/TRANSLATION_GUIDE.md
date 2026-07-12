## Translation Guide

This guide will explain how to translate the Interactive Emotes mod into different languages, including how to use special tokens to create dynamic dialogue.

**Example:**
```json
"greeting.hi.normal1": "Hello. How are you today?"
```
* **"greeting.hi.normal1"** is the **Key:** This is an identifier that the code uses to reference the text. **You must never change this Key.**
* **"Hello. How are you today?"** is the **Value:** This is the text that will be displayed in the game. This is the part you need to **translate into your language.**

> **How to Add a New Language:** To add a new language (e.g., Japanese), simply make a copy of `default.json`, rename it to `ja.json`, and then begin translating the text within that new file.

---

### **Special Tokens & Syntax**

Within the text values, you can use these special tokens to display dynamic game data:

|        Token       | Description                                                                  | Example in JSON                                         | Example In-Game Result                                                                  |
|:------------------:|------------------------------------------------------------------------------|---------------------------------------------------------|-----------------------------------------------------------------------------------------|
| `@`                | Replaced with the **player's name**.                                         | `"Hello, @!"`                                           | `Hello, Shiro!`                                                                         |
| `%farm%`           | Replaced with the player's **farm name**.                                    | `"How is %farm% farm?"`                                 | `How is Echo farm?`                                                                     |
| `%favorite_thing%` | Replaced with the player's **favorite thing**.                               | `"Let's talk about %favorite_thing%."`                  | `Let's talk about Pizza.`                                                               |
| `%pet%`            | Replaced with the player's **pet's name**.                                   | `"How is %pet% doing?"`                                 | `How is Mochi doing?`                                                                   |
| `%spouse%`         | Replaced with the speaking NPC's **spouse's name** (usually the player).     | `"Thank you, %spouse%."`                                | `Thank you, Shiro.`                                                                     |
| `^`                | **Gender Token**<br>`text_for_male_player^text_for_female_player`            | `"He's a nice guy.^She's a nice gal."`                  | `He's a nice guy.` (if player is male)`She's a nice gal.` (if player is female)         |
| `\|`               | **Dialogue Splitter Token**<br>Splits long text to be displayed in sequence. | `"The weather is nice today.\|Shall we go for a walk?"` | Shows "The weather is nice today." first, then "Shall we go for a walk?" after a pause. |

### **A Note on Spacing**

Spacing is important for making sentences appear natural.

* **Around standard tokens (`@`, `%farm%`):** Spacing works just like a normal sentence. The token will be replaced perfectly with the value.
    * **Good Example ✅:** `"Hello, @! The weather is nice."` -> `Hello, Link! The weather is nice.`
    * **Bad Example ❌:** `"Hello, @ ! The weather is nice."` -> `Hello, Link ! The weather is nice.` (This will result in an extra space before the `!`)

* **Around the Dialogue Splitter Token (`|`):**
    * This token splits the string exactly where it is placed. For consistency, it is best to write it without spaces on either side.
        * **Recommended:** `"First sentence.|Second sentence."`

---

### **Tips for Translators**

* **Never Change the Keys:** To reiterate, do not change the text on the left side of the colon (`:`).
* **Use Other Files as a Reference:** If you are unsure how to use a token, you can always open `default.json` or `th.json` to see examples.

Thank you for helping to translate this mod ♪(´▽｀) <br><br><br><br>