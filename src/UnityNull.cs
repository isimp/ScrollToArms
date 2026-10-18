namespace ScrollToArms
{
    /// <summary>
    /// Unity holds an object it destroyed, or one never there, to be none through its own ==, which
    /// ?., ?? and ??= skip: they see the C# object still standing and go on to use it. Going
    /// through OrNull first makes Unity's none a real null for them.
    /// </summary>
    internal static class UnityNull
    {
        /// <summary>The object, or null where Unity holds it to be none.</summary>
        public static T OrNull<T>(this T thing) where T : UnityEngine.Object => thing != null ? thing : null;
    }
}
