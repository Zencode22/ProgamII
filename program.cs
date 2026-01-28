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
     * 1️⃣ ITEM – the building block for everything that can be stored,
     *    crafted, bought or sold.
     *=====================================================================*/
    public enum ItemCategory { Material, Consumable, Weapon, Tool, Quest, Misc }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    public sealed class Item : IEquatable<Item>
    {
        public Guid Id { get; init; } = Guid.NewGuid();   // unique identifier
        public string Name { get; init; }                // e.g. "Milk"
        public string Description { get; init; } = "";   // tooltip text (optional)
        public string Unit { get; init; }                // e.g. "cups", "ounces"
        public decimal BasePrice { get; private set; }   // price for 1 unit
        public bool Stackable { get; init; } = true;     // can multiple units share a slot?
        public int MaxStackSize { get; init; } = 99;     // max per stack (if stackable)
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

        public string GetDisplayName() => Name;
        public string GetFullDescription() => $"{Name} ({Unit}) – {Description}";

        public decimal CalculateSellPrice(decimal vendorBuyModifier) =>
            Math.Round(BasePrice * vendorBuyModifier, 2);

        public decimal CalculateBuyPrice(decimal vendorSellModifier) =>
            Math.Round(BasePrice * vendorSellModifier, 2);

        public bool CanStackWith(Item other) =>
            other != null && Stackable && other.Stackable && Id == other.Id;

        public Item Clone() => (Item)MemberwiseClone();

        public bool Equals(Item? other) => other != null && Id == other.Id;
        public override bool Equals(object? obj) => Equals(obj as Item);
        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString() => $"{Name} ({Unit})";

        // Optional helper for sales events
        public void ApplyDiscount(decimal percent)
        {
            if (percent < 0m || percent > 100m)
                throw new ArgumentOutOfRangeException(nameof(percent));
            BasePrice = Math.Round(BasePrice * (1m - percent / 100m), 2);
        }
    }

    /*======================================================================
     * 2️⃣ QUANTITY – couples an Item with an amount.
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

        public bool Equals(Quantity? other)
        {
            if (other is null) return false;
            return Item.Id == other.Item.Id && Amount == other.Amount;
        }
        public override bool Equals(object? obj) => Equals(obj as Quantity);
        public override int GetHashCode() => HashCode.Combine(Item.Id, Amount);
    }

    /*======================================================================
     * 3️⃣ INGREDIENT & RESULT – semantic wrappers around Quantity.
     *=====================================================================*/
    public sealed class Ingredient : Quantity
    {
        public Ingredient(Item item, decimal amount) : base(item, amount) { }
    }

    public sealed class Result : Quantity
    {
        public Result(Item item, decimal amount) : base(item, amount) { }
    }

    /*======================================================================
     * 4️⃣ RECIPE – defines how to turn ingredients into a result.
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

        /// <summary>
        /// Checks whether the supplied inventory holds enough of every ingredient.
        /// </summary>
        public bool CanCraft(Inventory inventory)
        {
            foreach (var ing in Ingredients)
                if (!inventory.Has(ing.Item.Id, ing.Amount))
                    return false;
            return true;
        }

        /// <summary>
        /// Executes the craft: consumes ingredients and adds the result.
        /// Returns true on success, false if inventory lacked ingredients.
        /// </summary>
        public bool Craft(Inventory inventory)
        {
            if (!CanCraft(inventory)) return false;

            foreach (var ing in Ingredients)
                inventory.Remove(ing.Item.Id, ing.Amount);

            inventory.Add(Result.Item.Id, Result.Amount);
            return true;
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Name))
                return Fail(out error, "Recipe name missing.");
            if (Result == null)
                return Fail(out error, "Result not defined.");
            if (!Ingredients.Any())
                return Fail(out error, "At least one ingredient required.");

            foreach (var ing in Ingredients)
                if (ing.Amount <= 0)
                    return Fail(out error,
                                $"Ingredient {ing.Item.Name} has non‑positive amount.");

            error = string.Empty;
            return true;
        }

        private static bool Fail(out string err, string msg) { err = msg; return false; }

        public override string ToString()
        {
            var ingList = string.Join(", ", Ingredients.Select(i => i.ToString()));
            return $"{Name} → {Result.Amount} {Result.Item.Unit} {Result.Item.Name} (needs {ingList})";
        }
    }

    /*======================================================================
     * 5️⃣ INVENTORY – simple container mapping ItemId → amount.
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
     * 6️⃣ RECIPE CATALOG – creates the three original starter recipes
     *    plus the **new material “Sugar”** and a new recipe that uses it.
     *=====================================================================*/
    public static class RecipeCatalog
    {
        public static List<Recipe> LoadStarterRecipes()
        {
            // -----------------------------------------------------------------
            // Define reusable Items (materials & results)
            // -----------------------------------------------------------------
            var milk          = new Item("Milk",          "cups",   0.50m);
            var chocolateChip = new Item("Chocolate Chips", "cup", 1.20m);
            var hotChocolate  = new Item("Hot Chocolate", "ounces", 2.00m);

            var flour = new Item("Flour", "cups", 0.30m);
            var water = new Item("Water", "cups", 0.00m);
            var yeast = new Item("Yeast", "cup",  0.80m);
            var bread = new Item("Bread", "loaf", 1.50m);

            var herb          = new Item("Herb",          "pieces", 0.40m);
            var healingPotion = new Item("Healing Potion", "bottle", 3.00m);

            // *** NEW MATERIAL ***
            var sugar = new Item("Sugar", "cups", 0.60m);   // <-- added material

            // -----------------------------------------------------------------
            // 1️⃣ Hot Chocolate
            // -----------------------------------------------------------------
            var hotChocolateRecipe = new Recipe(
                name: "Hot Chocolate",
                result: new Result(hotChocolate, 12m),   // 12 ounces
                ingredients: new[]
                {
                    new Ingredient(milk,          4m),   // 4 cups milk
                    new Ingredient(chocolateChip, 0.5m)  // ½ cup chocolate chips
                },
                isStarter: true);

            // -----------------------------------------------------------------
            // 2️⃣ Bread
            // -----------------------------------------------------------------
            var breadRecipe = new Recipe(
                name: "Bread",
                result: new Result(bread, 1m),          // 1 loaf
                ingredients: new[]
                {
                    new Ingredient(flour, 3m),   // 3 cups flour
                    new Ingredient(water, 1.5m), // 1.5 cups water
                    new Ingredient(yeast, 0.02m) // 0.02 cup yeast
                },
                isStarter: true);

            // -----------------------------------------------------------------
            // 3️⃣ Healing Potion
            // -----------------------------------------------------------------
            var potionRecipe = new Recipe(
                name: "Healing Potion",
                result: new Result(healingPotion, 1m),   // 1 bottle
                ingredients: new[]
                {
                    new Ingredient(herb, 2m),   // 2 pieces herb
                    new Ingredient(water,0.5m)  // ½ cup water
                },
                isStarter: true);

            // -----------------------------------------------------------------
            // 4️⃣ Sweet Hot Chocolate (NEW RECIPE USING SUGAR)
            // -----------------------------------------------------------------
            var sweetHotChocolateRecipe = new Recipe(
                name: "Sweet Hot Chocolate",
                result: new Result(hotChocolate, 12m),   // same result item, same amount
                ingredients: new[]
                {
                    new Ingredient(milk,          4m),   // 4 cups milk
                    new Ingredient(chocolateChip, 0.5m), // ½ cup chocolate chips
                    new Ingredient(sugar,         0.25m) // ¼ cup sugar (new material)
                },
                isStarter: false); // not part of the initial three

            // Return the full list (starter + the new one)
            return new List<Recipe>
            {
                hotChocolateRecipe,
                breadRecipe,
                potionRecipe,
                sweetHotChocolateRecipe   // <-- included
            };
        }
    }

    /*======================================================================
     * 7️⃣ PROGRAM – entry point that demonstrates the crafting system.
     *=====================================================================*/
    internal static class Program
    {
        private static void Main()
        {
            // Load all recipes (including the new Sweet Hot Chocolate)
            var recipes = RecipeCatalog.LoadStarterRecipes();

            Console.WriteLine("=== All Recipes ===");
            foreach (var r in recipes) Console.WriteLine(r);
            Console.WriteLine();

            // Build a player inventory that can craft every recipe once
            var inventory = new Inventory();
            inventory.Add("milk",          10m);
            inventory.Add("chocolate_chip",2m);
            inventory.Add("flour",         5m);
            inventory.Add("water",         5m);
            inventory.Add("yeast",         0.1m);
            inventory.Add("herb",          5m);
            inventory.Add("sugar",         1m);   // enough for Sweet Hot Chocolate

            // Try to craft each recipe
            foreach (var recipe in recipes)
            {
                Console.WriteLine($"Attempting to craft: {recipe.Name}");
                if (recipe.Craft(inventory))
                {
                    var res = recipe.Result;
                    Console.WriteLine(
                        $"  SUCCESS – you now have {inventory.GetAmount(res.Item.Id)} {res.Item.Unit} {res.Item.Name}");
                }
                else
                {
                    Console.WriteLine("  FAILED – insufficient ingredients");
                }
                Console.WriteLine();
            }

            // Final inventory snapshot (show only items with a positive amount)
            Console.WriteLine("=== Final Inventory ===");
            foreach (var kvp in inventory.Contents.Where(k => k.Value > 0))
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
    }
}