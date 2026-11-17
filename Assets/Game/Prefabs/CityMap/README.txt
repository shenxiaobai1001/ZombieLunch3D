City Map 占位 + 流式地块
=======================

比例尺：1 = 25m｜路宽 6m｜过道 3m（相对角色缩小一半）
规则：地块之间隔马路；地块内过道；空位=开放设施。

流式生成（CityLotManager）
--------------------------
- 按格子灵活放置 1x1 / 2x1 / 1x2 / 2x2（不是固定只刷 2x2）
- 多格地块内部合并，不在地块内插马路
- 地块东/北侧自动铺路；边缘补西/南路
- 玩家在当前地块朝某方向走过约 1/3 时，该方向预生成新地块
- 地面（LotPad）与马路带 BoxCollider

使用步骤
--------
1. 菜单 Game → City Map → Generate Placeholder Prefabs
2. 菜单 Game → City Map → Create Lot Manager In Scene
3. 把 CityLotManager.Player 拖成你的角色（或 Tag=Player）
4. Play

Resources
---------
CityBuildingCatalog.asset      建筑/设施库
CityLotLayoutLibrary.asset     各地块尺寸的布局池

扩展
----
- 新布局：做 Layout 预制体（槽位+过道）→ 加进 LayoutLibrary
- 新建筑：做 Building 预制体 → 加进 BuildingCatalog
