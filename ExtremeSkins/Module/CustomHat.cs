﻿using ExtremeSkins.Module.Interface;
using UnityEngine;

using ExtremeRoles.Performance;

namespace ExtremeSkins.Module
{

#if WITHHAT
    public sealed class CustomHat : ICustomCosmicData<HatData>
    {
        public const string FrontImageName = "front.png";
        public const string FrontFlipImageName = "front_flip.png";
        public const string BackImageName = "back.png";
        public const string BackFlipImageName = "back_flip.png";
        public const string ClimbImageName = "climb.png";

        public HatData Data
        { 
            get => this.hat; 
        }

        public string Author
        { 
            get => this.author;
        }
        public string Name
        { 
            get => this.name; 
        }

        public string Id
        { 
            get => $"hat_{name}"; 
        }

        private bool hasFrontFlip;
        private bool hasBackFlip;

        private bool hasShader;
        private bool hasBack;
        private bool hasClimb;
        private bool isBounce;

        private string folderPath;

        private string name;
        private string author;

        private HatData hat;

        public CustomHat(
            string folderPath,
            string author,
            string name,
            bool hasFrontFlip,
            bool hasBack,
            bool hasBackFlip,
            bool hasClimb,
            bool isBounce,
            bool hasShader)
        {
            this.folderPath = folderPath;
            this.author = author;
            this.name = name;
            
            this.hasFrontFlip = hasFrontFlip;
            this.hasBack = hasBack;
            this.hasBackFlip = hasBackFlip;
            this.hasClimb = hasClimb;
            this.hasShader = hasShader;

            this.isBounce = isBounce;
        }

        public HatData GetData()
        {
            if (this.hat != null) { return this.hat; }

            this.hat = ScriptableObject.CreateInstance<HatData>();

            this.hat.name = Helper.Translation.GetString(this.Name);
            this.hat.displayOrder = 99;
            this.hat.ProductId = this.Id;
            this.hat.InFront = !this.hasBack;
            this.hat.NoBounce = !this.isBounce;
            this.hat.ChipOffset = new Vector2(0f, 0.2f);
            this.hat.Free = true;
            this.hat.NotInStore = true;

            this.hat.hatViewData.viewData = ScriptableObject.CreateInstance<HatViewData>();

            this.hat.hatViewData.viewData.MainImage = loadHatSprite(
                string.Concat(this.folderPath, @"\", FrontImageName));
            
            if (this.hasFrontFlip)
            {
                this.hat.hatViewData.viewData.LeftMainImage = loadHatSprite(
                    string.Concat(this.folderPath, @"\", FrontFlipImageName));
            }

            if (this.hasBack)
            {
                this.hat.hatViewData.viewData.BackImage = loadHatSprite(
                    string.Concat(this.folderPath, @"\", BackImageName));
            }
            if (this.hasBackFlip)
            {
                this.hat.hatViewData.viewData.LeftBackImage = loadHatSprite(
                    string.Concat(this.folderPath, @"\", BackFlipImageName));
            }

            if (this.hasClimb)
            {
                this.hat.hatViewData.viewData.ClimbImage = loadHatSprite(
                    string.Concat(this.folderPath, @"\", ClimbImageName));
            }

            if (this.hasShader)
            {
                Material altShader = new Material(
                    FastDestroyableSingleton<HatManager>.Instance.PlayerMaterial);
                altShader.shader = Shader.Find("Unlit/PlayerShader");

                this.hat.hatViewData.viewData.AltShader = altShader;
            }

            return this.hat;

        }

        private Sprite loadHatSprite(
            string path)
        {
            Texture2D texture = Loader.LoadTextureFromDisk(path);
            if (texture == null)
            {
                return null;
            }
            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.53f, 0.575f), texture.width * 0.375f);
            if (sprite == null)
            {
                return null;
            }
            texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
#endif
}