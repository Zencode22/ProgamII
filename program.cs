/*
 *Craft System*
 Your Name
 *Application created in PROG 201 Programming I*
 With code demos from instructor
 *Spring 2025*
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CraftingEngine
{
    /*======================================================================
     * 1️⃣ ITEM – fundamental building block.
     *=====================================================================*/
    public enum ItemCategory { Material, Consumable, Weapon, Tool, Quest, Misc }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    public sealed class Item : IEquatable<Item>
    {
        public Guid Id { get; init; } = Guid.NewGuid();   // immutable identifier
        public string Name { get; init; }
        public string Description { get; init; } = "";
        public string Unit { get; init; }
        public decimal BasePrice { get; private set; }
        public bool Stackable { get; init; } = true;
        public int MaxStackSize { get; init; } = 99;
        public ItemCategory Category { get; init; } = ItemCategory.Material;
        public Rarity Rarity { get; init; } = Rarity.Common;

        public Item(string name,
                    string unit,
                    decimal basePrice,
                    bool stackable = true,
                    int maxStackSize = 99,
                    ItemCategory category = ItemCategory.Material,
                    Rarity rarity = Rarity.Common,
                    string description = "")
        {
            Name        = name ?? throw new ArgumentNullException(nameof(name));
            Unit        = unit ?? throw new ArgumentNullException(nameof(unit));
            BasePrice   = basePrice;
            Stackable   = stackable;
            MaxStackSize= maxStackSize;
            Category    = category;
            Rarity      = rarity;
            Description = description;
        }

        // --------------------------------------------------------------
        // Required implementation of IEquatable<Item>
        // --------------------------------------------------------------
        public bool Equals(Item? other)
        {
            if (other is null) return false;
            return Id == other.Id;
        }

        // --------------------------------------------------------------
        // Standard overrides – keep them as‑is.
        // --------------------------------------------------------------
        public override bool Equals(object? obj) => Equals(obj as Item);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"{Name} ({Unit})";

        // Optional helper for discount scenarios
        public void ApplyDiscount(decimal percent)
        {
            if (percent < 0m || percent > 100m)
                throw new ArgumentOutOfRangeException(nameof(percent));
            BasePrice = Math.Round(BasePrice * (1m - percent / 100m), 2);
        }
    }

    /*======================================================================
     * 2️⃣ QUANTITY – immutable pairing of Item and amount.
     *=====================================================================*/
    public sealed class Quantity : IEquatable<Quantity>
    {
        public Item Item   { get; }
        public decimal Amount { get; }

        public Quantity(Item item, decimal amount)
        {
            Item   = item ?? throw new ArgumentNullException(nameof(item));
            if (amount <= 0) throw new ArgumentException("Amount must be > 0", nameof(amount));
            Amount = amount;
        }

        public override string ToString()
            => $"{Amount.ToString(CultureInfo.InvariantCulture)} {Item.Unit} {Item.Name}";

        public bool Equals(Quantity? other) =>
            other != null && Item.Id == other.Item.Id && Amount == other.Amount;

        public override bool Equals(object? obj) => Equals(obj as Quantity);
        public override int GetHashCode() => HashCode.Combine(Item.Id, Amount);
    }

    /*======================================================================
     * 3️⃣ INGREDIENT & RESULT – thin wrappers around Quantity.
     *=====================================================================*/
    public sealed class Ingredient
    {
        public Quantity Value { get; }
        public Ingredient(Item item, decimal amount) => Value = new Quantity(item, amount);
        public Item Item => Value.Item;
        public decimal Amount => Value.Amount;
        public override string ToString() => Value.ToString();
    }

    public sealed class Result
    {
        public Quantity Value { get; }
        public Result(Item item, decimal amount) => Value = new Quantity(item, amount);
        public Item Item => Value.Item;
        public decimal Amount => Value.Amount;
        public override string ToString() => Value.ToString();
    }

    /*======================================================================
     * 4️⃣ RECIPE – defines how ingredients become a result.
     *=====================================================================*/
    public sealed class Recipe
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; init; }
        public Result Result { get; init; }
        public IReadOnlyList<Ingredient> Ingredients { get; init; }
        public bool IsStarter { get; init; }

        public Recipe(string name,
                      Result result,
                      IEnumerable<Ingredient> ingredients,
                      bool isStarter = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name required", nameof(name));

            Name        = name;
            Result      = result ?? throw new ArgumentNullException(nameof(result));
            Ingredients = ingredients?.ToList().AsReadOnly()
                         ?? throw new ArgumentNullException(nameof(ingredients));
            IsStarter   = isStarter;
        }

        public bool CanCraft(Inventory inv) =>
            Ingredients.All(i => inv.Has(i.Item.Id.ToString(), i.Amount));

        public bool Craft(Inventory inv)
        {
            if (!CanCraft(inv)) return false;

            foreach (var ing in Ingredients)
                inv.Remove(ing.Item.Id.ToString(), ing.Amount);

            inv.Add(Result.Item.Id.ToString(), Result.Amount);
            return true;
        }

        public override string ToString()
        {
            var ingList = string.Join(", ", Ingredients.Select(i => i.ToString()));
            return $"{Name} → {Result.Amount} {Result.Item.Unit} {Result.Item.Name} (needs {ingList})";
        }
    }

    /*======================================================================
     * 5️⃣ INVENTORY – simple string‑keyed map of IDs to amounts.
     *=====================================================================*/
    public sealed class Inventory
    {
        private readonly Dictionary<string, decimal> _store =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, decimal> Contents => _store;

        public void Add(string itemId, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("ItemId required", nameof(itemId));
            if (amount <= 0) throw new ArgumentException("Amount must be > 0", nameof(amount));

            if (_store.ContainsKey(itemId))
                _store[itemId] += amount;
            else
                _store[itemId] = amount;
        }

        public bool Remove(string itemId, decimal amount)
        {
            if (!_store.TryGetValue(itemId, out var have) || have < amount)
                return false;

            _store[itemId] = have - amount;
            if (_store[itemId] == 0) _store.Remove(itemId);
            return true;
        }

        public bool Has(string itemId, decimal amount) =>
            _store.TryGetValue(itemId, out var have) && have >= amount;

        public decimal GetAmount(string itemId) =>
            _store.TryGetValue(itemId, out var amt) ? amt : 0m;
    }

    /*======================================================================
     * 6️⃣ CENTRALIZED ITEM DEFINITIONS
     *=====================================================================*/
    public static class GameItems
    {
        // Core materials & consumables
        public static readonly Item Milk          = new Item("Milk",          "cups",   0.50m);
        public static readonly Item ChocolateChip = new Item("Chocolate Chips","cup",    1.20m);
        public static readonly Item HotChocolate  = new Item("Hot Chocolate", "ounces", 2.00m);
        public static readonly Item Flour         = new Item("Flour",         "cups",   0.30m);
        public static readonly Item Water         = new Item("Water",         "cups",   0.00m);
        public static readonly Item Yeast         = new Item("Yeast",         "cup",    0.80m);
        public static readonly Item Bread         = new Item("Bread",         "loaf",   1.50m);
        public static readonly Item Herb          = new Item("Herb",          "pieces", 0.40m);
        public static readonly Item HealingPotion = new Item("Healing Potion","bottle", 3.00m);
        public static readonly Item Sugar         = new Item("Sugar",         "cups",   0.60m); // newly added material
    }

    /*======================================================================
     * 7️⃣ RECIPE CATALOG – builds recipes using the shared items.
     *=====================================================================*/
    public static class RecipeCatalog
    {
        public static List<Recipe> LoadStarterRecipes()
        {
            // Short aliases for readability
            var m  = GameItems.Milk;
            var cc = GameItems.ChocolateChip;
            var hc = GameItems.HotChocolate;
            var f  = GameItems.Flour;
            var w  = GameItems.Water;
            var y  = GameItems.Yeast;
            var b  = GameItems.Bread;
            var h  = GameItems.Herb;
            var hp = GameItems.HealingPotion;
            var s  = GameItems.Sugar;

            var hotChocolateRecipe = new Recipe(
                name: "Hot Chocolate",
                result: new Result(hc, 12m),
                ingredients: new[]
                {
                    new Ingredient(m, 4m),
                    new Ingredient(cc, 0.5m)
                },
                isStarter: true);

            var breadRecipe = new Recipe(
                name: "Bread",
                result: new Result(b, 1m),
                ingredients: new[]
                {
                    new Ingredient(f, 3m),
                    new Ingredient(w, 1.5m),
                    new Ingredient(y, 0.02m)
                },
                isStarter: true);

            var potionRecipe = new Recipe(
                name: "Healing Potion",
                result: new Result(hp, 1m),
                ingredients: new[]
                {
                    new Ingredient(h, 2m),
                    new Ingredient(w, 0.5m)
                },
                isStarter: true);

            var sweetHotChocolateRecipe = new Recipe(
                name: "Sweet Hot Chocolate",
                result: new Result(hc, 12m),
                ingredients: new[]
                {
                    new Ingredient(m, 4m),
                    new Ingredient(cc, 0.5m),
                    new Ingredient(s, 0.25m) // uses the new Sugar material
                },
                isStarter: false);

            return new List<Recipe>
            {
                hotChocolateRecipe,
                breadRecipe,
                potionRecipe,
                sweetHotChocolateRecipe
            };
        }
    }

    /*======================================================================
     * 8️⃣ PROGRAM – entry point.
     *=====================================================================*/
    internal static class Program
    {
        private static void Main()
        {
            // Load recipes (they reference the shared GameItems)
            var recipes = RecipeCatalog.LoadStarterRecipes();

            Console.WriteLine("=== All Recipes ===");
            foreach (var r in recipes) Console.WriteLine(r);
            Console.WriteLine();

            // Populate inventory using the same Item instances
            var inventory = new Inventory();
            inventory.Add(GameItems.Milk.Id.ToString(),          10m);
            inventory.Add(GameItems.ChocolateChip.Id.ToString(),  2m);
            inventory.Add(GameItems.Flour.Id.ToString(),         5m);
            inventory.Add(GameItems.Water.Id.ToString(),         5m);
            inventory.Add(GameItems.Yeast.Id.ToString(),         0.1m);
            inventory.Add(GameItems.Herb.Id.ToString(),          5m);
            inventory.Add(GameItems.Sugar.Id.ToString(),         1m); // needed for Sweet Hot Chocolate

            // Attempt to craft each recipe
            foreach (var recipe in recipes)
            {
                Console.WriteLine($"Attempting to craft: {recipe.Name}");
                if (recipe.Craft(inventory))
                {
                    var res = recipe.Result;
                    Console.WriteLine(
                        $"  SUCCESS – you now have {inventory.GetAmount(res.Item.Id.ToString())} {res.Item.Unit} {res.Item.Name}");
                }
                else
                {
                    Console.WriteLine("  FAILED – insufficient ingredients");
                }
                Console.WriteLine();
            }

            // Final inventory snapshot (only items with a positive amount)
            Console.WriteLine("=== Final Inventory ===");
            foreach (var kvp in inventory.Contents.Where(k => k.Value > 0))
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
    }
}