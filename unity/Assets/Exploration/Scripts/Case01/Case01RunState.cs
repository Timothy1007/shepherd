namespace Shepherd.Exploration.Case01
{
    /// <summary>Demo 切片的區域：事務所、十七號門外的街區、十七號屋內。</summary>
    public enum Case01DemoArea
    {
        Office,
        Street,
        House,
    }

    /// <summary>
    /// v0.5 Demo 切片的跨區域進度（事務所 → 街區 → 十七號 → 街區 → 事務所）。
    /// 屋內的完整進度（旗標、日誌、核的狀態、FP-001）就是同一個 <see cref="Case01Flow"/> 物件，換區域時原封不動地帶著走，
    /// 不會因為離開屋子而遺失。只存在於本次執行的記憶體中（還沒有存檔系統）。
    /// </summary>
    public static class Case01RunState
    {
        public static bool CaseAccepted;
        public static bool StreetSurveyed;
        public static bool SawListing;
        public static bool SawPlate17;
        public static bool EnteredHouse;
        public static bool CaseClosed;
        /// <summary>屋內進度（第一次進屋時建立；之後每次進出都沿用同一個）。</summary>
        public static Case01Flow HouseFlow;
        /// <summary>Demo 使用的一樓格局（預設原版；選單可選 v0.5 方案 A）。</summary>
        public static Case01Floorplan Floorplan = Case01Floorplan.Original;
        /// <summary>核校正規則（Demo 使用 v0.3 關係校正）。</summary>
        public static Case01CorrectionMode Correction = Case01CorrectionMode.RevisedV03;

        public static bool HouseResolved { get { return HouseFlow != null && HouseFlow.CoreStabilized; } }
        public static bool FamilyPhotoCollected { get { return HouseFlow != null && HouseFlow.FamilyPhotoCollected; } }

        public static void Reset(Case01Floorplan plan = Case01Floorplan.Original)
        {
            CaseAccepted = false;
            StreetSurveyed = false;
            SawListing = false;
            SawPlate17 = false;
            EnteredHouse = false;
            CaseClosed = false;
            HouseFlow = null;
            Floorplan = plan;
            Correction = Case01CorrectionMode.RevisedV03;
        }

        /// <summary>第一次進屋時建立屋內流程（從玄關開始）。</summary>
        public static Case01Flow EnsureHouseFlow()
        {
            if (HouseFlow == null) HouseFlow = Case01Flow.ForCheckpoint(Case01Checkpoint.S00, Correction);
            return HouseFlow;
        }
    }
}
