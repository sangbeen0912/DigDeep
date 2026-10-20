# 신규 리소스와 생성 프롬프트

생성 방식: builtin image_gen. CLI/API 대체 경로를 사용하지 않았다. 아래 세 이미지를 프로젝트에 복사하고 프리팹·씬에 연결했다. 원본 픽셀은 수정하지 않고 유니티 슬라이스, 투명 여백, 표시 비율로 조정했다.

## 저장 위치
- 다이아 광석: `C:/프로젝트모음/DigDeep_Project/DigDeep/DigDeep/Assets/DigDeep/Art/DiamondOre.png`
- 다이아 곡괭이: `C:/프로젝트모음/DigDeep_Project/DigDeep/DigDeep/Assets/DigDeep/Art/DiamondPick.png`
- 변환기: `C:/프로젝트모음/DigDeep_Project/DigDeep/DigDeep/Assets/DigDeep/Art/EnergyConverter.png`

다이아 곡괭이는 기존 돌 곡괭이의 프레임 비율 504:479에 맞춘다. 세 리소스는 최대 512 텍스처, Point 필터, Mipmap 없음, 런타임 읽기 없음이다. 석유는 별도 텍스처가 필요 없는 검은 Image 사각형이다. 기존 시트들은 여전히 사용하는 자산이므로 보존한다.

## 다이아 광석
참조: Ores-v2.png. 배경 투명: false.

Use case: stylized-concept. Asset type: a single square ore tile for DigDeep, a Minecraft-inspired 2D pixel mining mobile game. Create ONE front-facing square gray stone tile with ONE LARGE cyan diamond gemstone embedded in the very center. The faceted angular cyan diamond fills about 45 percent of tile width, with crisp white/aqua highlights, dark teal pixel edges; leave broad empty gray-stone margins on all four sides. Stone background made of sparse coarse square gray pixels, same simple palette and pixel scale as the supplied stone ore reference. Clearly one central big jewel, not many small mineral flecks. Entire image is exactly the tile, edge to edge with square corners, no border/frame, no transparency, no lettering, no icons around it, no perspective, no glow spilling out of tile, crisp nearest-neighbor pixel art. Reference image is ONLY for the stone material and pixel scale; deliver a new single diamond tile, not an edited three-tile sheet.

## 다이아 곡괭이
참조: Pickaxes-v3.png. 배경 투명: true.

Use case: stylized-concept. Asset type: ONE isolated symmetrical diamond pickaxe sprite for the DigDeep pixel-art game. The supplied image is a STYLE/PROPORTION REFERENCE only, showing four existing pickaxes. Make a new SINGLE diamond pickaxe on a truly transparent background. Match exactly the second (stone) pickaxe's silhouette, head width versus handle length, wooden handle thickness and length, overall square bounding box, and coarse pixel grid. Pickaxe is upright with a vertical centered brown wooden handle. Wide double-ended curved turquoise/cyan diamond head, with BOTH left and right tips, mirror-symmetrical head outline around the vertical handle centerline. Both head halves have exactly the same reach, height and stepped contour. Head spans nearly full sprite width; from the top of the head to the handle bottom the total height is about the same as the head width. Neither longer handle nor an angled or one-sided Minecraft axe. Large coarse crisp squares, cyan highlights, dark teal outline, brown handle. No extra items, no text, no background, no shadow, no ground plane, no particles, no glow. Reference must NOT be reproduced as a four-pickaxe sheet; output only this one new pickaxe.

## 공용 에너지 변환기
배경 투명: true.

Use case: stylized-concept. Asset type: ONE standalone compact general-purpose mineral-to-energy converter machine sprite for a 2D pixel-art mining game. It can take MANY kinds of minerals, not a coal-only furnace. Front-facing nearly square dark slate-metal machine body, brass corner rivets, one simple wide feed slot near the upper edge, a large bright cyan faceted energy core in a square central chamber, a small cyan energy gauge on one side. Coarse crisp square pixels like an indie Minecraft-inspired inventory icon, restrained gray/gold/cyan palette, substantial dark outline, readable at 90px. A clean compact device with a flat front, not an isometric scene. Truly transparent background, full device entirely inside the canvas with comfortable margin, no other objects or loose minerals, no coal, no flames, no chimney, no text, no numbers, no logo, no pedestal or backdrop, no wide glow outside the body. A neutral multi-mineral energy converter, not a shop building.
