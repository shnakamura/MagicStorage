using System.Collections.Generic;
using System.Linq;
using MagicStorage.Components;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using System;

namespace MagicStorage
{
	public class StoragePlayer : ModPlayer
	{
		public static StoragePlayer LocalPlayer => Main.LocalPlayer.GetModPlayer<StoragePlayer>();

		private readonly ItemTypeOrderedSet _craftedRecipes = new("CraftedRecipes");

		private readonly ItemTypeOrderedSet _hiddenRecipes = new("HiddenItems");

		private TEStorageHeart _latestAccessedStorage;
		public bool remoteAccess;
		private Point16 storageAccess = Point16.NegativeOne;

		public int timeSinceOpen = 1;

		public override bool CloneNewInstances => false;

		public IEnumerable<Item> HiddenRecipes => _hiddenRecipes.Items;

		public IEnumerable<Item> CraftedRecipes => _craftedRecipes.Items;

		public ItemTypeOrderedSet FavoritedRecipes { get; } = new("FavoritedRecipes");

		public ItemTypeOrderedSet SeenRecipes { get; } = new("SeenRecipes");

		public ItemTypeOrderedSet TestedRecipes { get; } = new("TestedRecipes");

		public ItemTypeOrderedSet AsKnownRecipes { get; } = new("AsKnownRecipes");

		public TEStorageHeart LatestAccessedStorage => _latestAccessedStorage is { IsAlive: true } ? _latestAccessedStorage : null;

		public bool IsRecipeHidden(Item item) => _hiddenRecipes.Contains(item);

		public bool AddToHiddenRecipes(Item item) => _hiddenRecipes.Add(item);

		public bool RemoveFromHiddenRecipes(Item item) => _hiddenRecipes.Remove(item);

		public bool AddToCraftedRecipes(Item item) => _craftedRecipes.Add(item);

		public override void SaveData(TagCompound tag)
		{
			_hiddenRecipes.Save(tag);
			_craftedRecipes.Save(tag);
			FavoritedRecipes.Save(tag);
			SeenRecipes.Save(tag);
			TestedRecipes.Save(tag);
			AsKnownRecipes.Save(tag);
		}

		public override void LoadData(TagCompound tag)
		{
			_hiddenRecipes.Load(tag);
			_craftedRecipes.Load(tag);
			FavoritedRecipes.Load(tag);
			SeenRecipes.Load(tag);
			TestedRecipes.Load(tag);
			AsKnownRecipes.Load(tag);
		}

		public override void UpdateDead()
		{
			if (Player.whoAmI == Main.myPlayer)
				CloseStorage();
		}

		public override void ResetEffects()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;

			if (timeSinceOpen < 1)
			{
				Player.SetTalkNPC(-1);
				Main.playerInventory = true;
				timeSinceOpen++;
			}

			if (storageAccess.X >= 0 && storageAccess.Y >= 0 && (Player.chest != -1 || !Main.playerInventory || Player.sign > -1 || Player.talkNPC > -1))
			{
				CloseStorage();
				Recipe.FindRecipes();
			}
			else if (storageAccess.X >= 0 && storageAccess.Y >= 0)
			{
				int playerX = (int)(Player.Center.X / 16f);
				int playerY = (int)(Player.Center.Y / 16f);
				if (!remoteAccess &&
					(playerX < storageAccess.X - Player.lastTileRangeX ||
					 playerX > storageAccess.X + Player.lastTileRangeX + 1 ||
					 playerY < storageAccess.Y - Player.lastTileRangeY ||
					 playerY > storageAccess.Y + Player.lastTileRangeY + 1))
				{
					SoundEngine.PlaySound(SoundID.MenuClose);
					CloseStorage();
					Recipe.FindRecipes();
				}
				else if (TileLoader.GetTile(Main.tile[storageAccess.X, storageAccess.Y].TileType) is not StorageAccess)
				{
					SoundEngine.PlaySound(SoundID.MenuClose);
					CloseStorage();
					Recipe.FindRecipes();
				}
			}
		}

		public void OpenStorage(Point16 point, bool remote = false)
		{
			storageAccess = point;
			remoteAccess = remote;
			_latestAccessedStorage = GetStorageHeart();

			if (MagicStorageConfig.UseConfigFilter && CraftingGUI.recipeButtons is not null)
			{
				CraftingGUI.recipeButtons.Choice = MagicStorageConfig.ShowAllRecipes ? 1 : 0;
			}

			if (MagicStorageConfig.ClearSearchText)
			{
				StorageGUI.searchBar?.Reset();
				CraftingGUI.searchBar?.Reset();
			}

			StorageGUI.RefreshItems();
		}

		public void CloseStorage()
		{
			storageAccess = Point16.NegativeOne;
			Main.blockInput = false;
		}

		public Point16 ViewingStorage() => storageAccess;

		public static void GetItem(IEntitySource source, Item item, bool toMouse)
		{
			Player player = Main.LocalPlayer;
			if (toMouse && Main.playerInventory && Main.mouseItem.IsAir)
			{
				Main.mouseItem = item;
				item = new Item();
			}
			else if (toMouse && Main.playerInventory && Main.mouseItem.type == item.type)
			{
				int total = Main.mouseItem.stack + item.stack;
				if (total > Main.mouseItem.maxStack)
					total = Main.mouseItem.maxStack;
				int difference = total - Main.mouseItem.stack;
				Main.mouseItem.stack = total;
				item.stack -= difference;
			}

			if (!item.IsAir)
			{
				item = player.GetItem(Main.myPlayer, item, GetItemSettings.InventoryEntityToPlayerInventorySettings);
				if (!item.IsAir && Main.mouseItem.IsAir)
				{
					Main.mouseItem = item;
					item = new Item();
				}

				if (!item.IsAir && Main.mouseItem.type == item.type && Main.mouseItem.stack < Main.mouseItem.maxStack)
				{
					Main.mouseItem.stack += item.stack;
					item = new Item();
				}

				if (!item.IsAir)
					player.QuickSpawnClonedItem(source, item, item.stack);
			}
		}

		public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
		{
			if (context != ItemSlot.Context.InventoryItem && context != ItemSlot.Context.InventoryCoin && context != ItemSlot.Context.InventoryAmmo)
				return false;
			if (storageAccess.X < 0 || storageAccess.Y < 0)
				return false;
			Item item = inventory[slot];
			if (item.favorited || item.IsAir)
				return false;
			int oldType = item.type;
			int oldStack = item.stack;
			if (StorageCrafting())
			{
				GetCraftingAccess().TryDepositStation(item);
			}
			else
			{
				GetStorageHeart().TryDeposit(item);
			}

			if (item.type != oldType || item.stack != oldStack)
			{
				SoundEngine.PlaySound(SoundID.Grab);
				StorageGUI.RefreshItems();
			}

			return true;
		}

		public TEStorageHeart GetStorageHeart()
		{
			if (storageAccess.X < 0 || storageAccess.Y < 0)
				return null;
			Tile tile = Main.tile[storageAccess.X, storageAccess.Y];
			if (!tile.HasTile)
				return null;
			ModTile modTile = TileLoader.GetTile(tile.TileType);
			return (modTile as StorageAccess)?.GetHeart(storageAccess.X, storageAccess.Y);
		}

		public TECraftingAccess GetCraftingAccess()
		{
			if (storageAccess.X < 0 || storageAccess.Y < 0)
				return null;

			if (TileEntity.ByPosition.TryGetValue(storageAccess, out TileEntity te))
				return te as TECraftingAccess;

			return null;
		}

		public bool StorageCrafting()
		{
			if (storageAccess.X < 0 || storageAccess.Y < 0)
				return false;
			Tile tile = Main.tile[storageAccess.X, storageAccess.Y];
			return tile.HasTile && tile.TileType == ModContent.TileType<CraftingAccess>();
		}

		public static bool IsStorageCrafting() => StoragePlayer.LocalPlayer.StorageCrafting();

		public override void ModifyHitByNPC(NPC npc, ref int damage, ref bool crit)
		{
			foreach (Item item in Player.inventory.Concat(Player.armor).Concat(Player.dye).Concat(Player.miscDyes).Concat(Player.miscEquips))
				if (item is not null && !item.IsAir && CraftingGUI.IsTestItem(item))
				{
					damage *= 5;
					break;
				}
		}

		public override bool CanHitPvp(Item item, Player target)
		{
			if (CraftingGUI.IsTestItem(item))
				return false;
			return base.CanHitPvp(item, target);
		}

		public override void OnRespawn(Player player)
		{
			foreach (Item item in player.inventory.Concat(player.armor).Concat(player.dye).Concat(player.miscDyes).Concat(player.miscEquips))
				if (item is not null && !item.IsAir && CraftingGUI.IsTestItem(item))
					item.TurnToAir();

			{
				Item item = player.trashItem;
				if (item is not null && !item.IsAir && CraftingGUI.IsTestItem(item))
					item.TurnToAir();
			}

			base.OnRespawn(player);
		}
	}
}
