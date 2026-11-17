using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>配置</summary>
public class Config
{
    public static int ClearType=1;

    /// <summary>横向移动右边界（固定）</summary>
    public static float MoveRightBound = 0f;
    public static float PlayerMoveRightBound = 6;
    public static float PlayerMoveLeftBound = -6;
    public static float CameraMoveLeftBound = 0f;


    public static Color32 skinColor1 = new Color32(206,170,156,255);

    public static Color32 skinColor2 = new Color32(186, 126, 103, 255);

    public static Color32 skinColor3 = new Color32(85, 50, 28, 255);

    public static Color32 OnGetSkinKid()
    {
        int index =UnityEngine.Random.Range(0, 4);
        switch (index)
        {
            case 0:
                return  skinColor1;
            case 1:
                return skinColor2;
            case 2:
                return skinColor3;
        }
        return Color.white;
    }
}
public enum MoveType
{
    Normal,
    HighLeft,
    HighRight,
    MaxLeft,
    MaxRight
}

public enum CarType
{
    Motorcycle,
    Sedan,
    Truck,
    SpecialVehicle,
}

public enum KidMoveType
{
    None = 0,
    MoveTarget,
    MoveTargetPause,
    Move,
    Pause,
    Hit,
    Pushed,
    Slip,
}

/// <summary>事件合集 </summary>
public enum Events
{
    None,
    KidPass,
    KidPause,
    BigenMoveBlock,
    OnModVideoPlayEnd,
    OnModVideoPlayStart,
    RoadCreateFinished,
    RoadDestroyFinished, 
    OnBarryExecutEnd,
    OnShowSystem,
    OnTargetOrderChanged,
    OnCustomTextChanged,
    OnNpcSpawnIntervalChanged,
    OnViewerOrderRankChanged,
    OnKidToRoad,
    OnKidEnd,
    OnSystemSettingsChanged,
    OnBreakBlock,
    OnChildDance,

    /// <summary>请求增加盘子库存 (PlateStockMsg)</summary>
    OnPlateStockAddRequest,
    /// <summary>请求减少盘子库存 (PlateStockMsg) — 做餐用</summary>
    OnPlateStockConsumeRequest,
    /// <summary>盘子库存已变化 (PlateStockMsg)</summary>
    OnPlateStockChanged,

    /// <summary>切好的土豆碎到达炸薯条机</summary>
    OnChipsArrivedAtFryer,
    /// <summary>炸薯条流程结束（薯条已补到盘子），土豆可再出现</summary>
    OnPotatoFryCycleDone,

    /// <summary>空杯到达接饮料位，开始出饮料 (BeverageFillMsg)</summary>
    OnBeverageFillStart,
    /// <summary>接饮料结束 (BeverageFillMsg)</summary>
    OnBeverageFillDone,

    /// <summary>拖放餐品尝试交付 (OrderFoodDeliverMsg)</summary>
    OnOrderFoodDeliverTry,
    /// <summary>餐品交付结果 (OrderFoodDeliverMsg.accepted)</summary>
    OnOrderFoodDeliverResult,
    /// <summary>出餐位被取走，可继续备货 (FoodSlotFreedMsg)</summary>
    OnFoodSlotFreed,
}

/// <summary>饮料类型</summary>
public enum BeverageType
{
    Fenda,
    Baishi,
}

/// <summary>接饮料消息</summary>
public class BeverageFillMsg
{
    public BeverageType type;
    public BeverageFillMsg(BeverageType type) { this.type = type; }
}
public enum BarrageState//功能状态
{
    Tigger,
    Ready,
    Underway,
    Pause,
    Finsh,
    Close,
}

public enum BarraegExecutType//功能处理状态
{
    ReadyExecut,
    Executing,
    ExecutEnd,
}