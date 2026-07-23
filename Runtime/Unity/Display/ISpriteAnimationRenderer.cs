namespace Combat.Unity.Display
{
    public interface ISpriteAnimationRenderer
    {
        void ApplyFrame(SpriteAnimationClipAsset clip, int frameIndex);
        void Clear();
    }
}
