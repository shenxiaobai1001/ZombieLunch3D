City Map 占位 + 流式地块
=======================

输出目录（已迁至此）：
Assets/Game/Resources_moved/Prefabs/Game/CityMap/

比例尺：1 = 25m｜路宽 6m｜过道 3m（相对角色缩小一半）
规则：地块之间隔马路；地块内过道；空位=开放设施。

子目录
------
Pieces/      底板、马路、过道
Buildings/   各 footprint 建筑类型
Facilities/  开放设施
Layouts/     地块槽位布局
Demo/        示例
Materials/   占位材质

使用步骤
--------
1. 菜单 Game → City Map → Generate Placeholder Prefabs
2. 菜单 Game → City Map → Create Lot Manager In Scene
3. Player Tag=Player 后 Play

建筑新类型放到 Buildings/B1x1 等对应目录，挂 CityBuildingVariant，
再拖进 Assets/Game/Resources/CityBuildingCatalog.asset。
