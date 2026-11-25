# BaiTap_VR — Unity 6000.0.23f1 (URP)

Dự án bài tập thực hành Thực tế ảo (VR) & Đồ họa 3D trên **Unity 6000.0.23f1** (Universal Render Pipeline, ProBuilder, Polybrush, Terrain).

## Các Scene trong `Assets/Scenes/`

1. **`BasicScene.unity`** — *Xây dựng cảnh quan thiên nhiên 3D (Skybox & Terrain)*
   - Terrain `500 × 500 × 100` gồm 5 ngọn núi/dãy núi, thung lũng trung tâm, dòng sông uốn lượn đổ ra hồ nước.
   - 3 lớp Terrain Texture (Cỏ, Đất, Đá kèm Normal Map), trồng rừng cây Asset Store bằng công cụ **Terrain Paint Trees** xung quanh hồ và sông.
   - Skybox `Skybox_BauTroi` (`Skybox/Procedural`), `Directional Light` nắng xiên bóng đổ mềm, `Main Camera` toàn cảnh.

2. **`BT_VR.unity`** — *Bài tập thực hành Tuần 4, 5, 6*
   - Ngôi nhà văn phòng 2 tầng `OfficeHouse` + Khung tranh treo tường + Cây cảnh bồn đất dựng bằng **ProBuilder**.
   - `PoliceCar_Animation` (*Asset Store Interceptor*): chạy vòng quanh nhà bằng Animation thuần hoặc Waypoint chỉnh sửa trực tiếp, có đèn pha, đèn nháy xanh-đỏ, hiệu ứng sáng và còi hú.
   - `PlayerCar` (*Asset Store Interceptor*): điều khiển quanh nhà bằng `WASD` / phím mũi tên; khi chạm `Barrier_1..3` sẽ bốc khói, phát âm thanh va chạm và dừng hẳn (`R` để reset).
   - Phím nhanh: `1 / 2 / 3` đổi góc nhìn camera, `M` đổi chế độ xe cảnh sát, `L` đèn, `K` còi, `R` reset xe.

3. **`KhungCanhTuongTac.unity`** — *Tạo khung cảnh tương tác trong Unity (Công viên Đảo Đôi)*
   - Terrain 2 đảo công viên + hồ nước xanh + rừng cây trồng bằng **Terrain Paint Trees** (*Happy Little Trees* + *BEDRILL*).
   - 4 công trình dựng bằng **ProBuilder** (Cầu vòm bắc qua hồ, Cối xay gió có Animation quay cánh, Tháp hải đăng, Sân đỗ trực thăng Helipad) + 2 gò hoa **Polybrush**.
   - Xe `Interceptor` lái qua cầu (`WASD` / mũi tên, `Space` phanh, `L` đèn, `K` còi, `R` reset), trực thăng `Helicopter` bay tuần tra bằng Animation, âm thanh sóng nước + gió + động cơ, Camera `1 / 2 (Cinematic Orbit) / 3`.

4. **`BTTH_06.unity`** — *Thung lũng núi tuyết & Đường xuyên rừng*
   - Có thể dựng lại bất cứ lúc nào từ menu Unity: **`Tools > HUBT > Build Scene BTTH_06 (Thung Lung Nui Tuyet)`**.

## Menu dựng nhanh trong Unity Editor
- `Tools > HUBT > Build BasicScene (Canh Quan Thien Nhien 3D)`
- `Tools > HUBT > Build Scene BT_VR (Tuan 4, 5, 6)`
- `Tools > HUBT > Build Scene BTTH_06 (Thung Lung Nui Tuyet)`
- `Tools > HUBT > Build Scene KhungCanhTuongTac (Cong Vien Dao Xanh)`
