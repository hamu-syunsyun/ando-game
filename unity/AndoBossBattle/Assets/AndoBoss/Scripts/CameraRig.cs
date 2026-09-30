using UnityEngine;

namespace AndoBoss
{
    // 三人称カメラ。マウスで回す＋ボスへの自動ロックオン（マウスを触らない人でも戦える）
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public float Yaw, Pitch = 0.3f, Dist = 8.5f;
        public bool LockOn = true;
        float mouseIdle = 99;
        float shake, fovPunch;
        const float BaseFov = 55;

        // 演出用のカメラ（奥義・撃破・タイトル）
        bool cine; Vector3 cinePos, cineLook; float cineFov = 50, cineBlend;
        Vector3 smoothTarget;

        public void Init()
        {
            Cam = gameObject.AddComponent<Camera>();
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 600;
            Cam.fieldOfView = BaseFov;
            Cam.allowHDR = true;
            Cam.allowMSAA = true;
            Cam.clearFlags = CameraClearFlags.Skybox;
            gameObject.tag = "MainCamera";
            gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<PostFX>();
        }

        public void Shake(float s) => shake = Mathf.Max(shake, s);
        public void FovPunch(float f) => fovPunch = f;

        public void Cinematic(Vector3 pos, Vector3 look, float fov, bool snap = false)
        {
            cine = true; cinePos = pos; cineLook = look; cineFov = fov;
            if (snap) cineBlend = 1;
        }
        public void EndCinematic() => cine = false;

        public void SnapBehindPlayer()
        {
            var G = Game.I;
            var toBoss = Player.Flat(G.Boss.Pos - G.Player.Pos);
            Yaw = Mathf.Atan2(toBoss.x, toBoss.z);
            Pitch = 0.3f;
            smoothTarget = G.Player.Pos + Vector3.up * 1.6f;
        }

        public void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            var G = Game.I;
            var P = G.Player; var B = G.Boss;

            if (G.State == Game.Mode.Battle && Cursor.lockState == CursorLockMode.Locked && !Fx.I.Paused)
            {
                var md = GameInput.MouseDelta();
                if (md.sqrMagnitude > 0.0001f) mouseIdle = 0;
                Yaw += md.x * 0.045f;
                Pitch = Mathf.Clamp(Pitch - md.y * 0.03f, -0.2f, 1.1f);
            }
            // コントローラーの右スティックでも視点を回せる
            if (G.State == Game.Mode.Battle && !Fx.I.Paused)
            {
                var ls = GameInput.LookStick();
                if (ls.sqrMagnitude > 0) { mouseIdle = 0; Yaw += ls.x * 2.6f * dt; Pitch = Mathf.Clamp(Pitch - ls.y * 1.5f * dt, -0.2f, 1.1f); }
            }
            mouseIdle += dt;
            if (GameInput.Down(GameInput.K.LockOn) && G.State == Game.Mode.Battle)
            {
                LockOn = !LockOn;
                G.Hud.Toast(LockOn ? "ロックオン：ON" : "ロックオン：OFF");
                Sfx.Play("tick");
            }

            // ロックオン中は、マウスを1.2秒触らなければボスの方へゆっくり向き直る
            if (LockOn && G.State == Game.Mode.Battle && B.Alive && mouseIdle > 1.2f)
            {
                var to = Player.Flat(B.Pos - P.Pos);
                if (to.magnitude > 2.5f)
                {
                    float want = Mathf.Atan2(to.x, to.z);
                    Yaw = Player.TurnTo(Yaw, want, dt * 2.2f);
                    float d = to.magnitude;
                    float wantPitch = Mathf.Lerp(0.38f, 0.22f, Mathf.InverseLerp(4, 16, d)) + (B.Y > 2 ? 0.2f : 0);
                    Pitch = Mathf.Lerp(Pitch, wantPitch, dt * 1.5f);
                }
            }

            var target = P.Pos + Vector3.up * 1.6f;
            smoothTarget = Vector3.Lerp(smoothTarget, target, 1 - Mathf.Exp(-dt * 12));
            var off = new Vector3(-Mathf.Sin(Yaw) * Mathf.Cos(Pitch), Mathf.Sin(Pitch), -Mathf.Cos(Yaw) * Mathf.Cos(Pitch)) * Dist;
            var pos = smoothTarget + off;
            if (pos.y < 0.4f) pos.y = 0.4f;
            var look = smoothTarget + Vector3.up * 0.2f;
            // ボスが近くにいるときは少しボスの方も画面に入れる
            if (G.State == Game.Mode.Battle && B.Alive)
            {
                var toB = B.Pos + Vector3.up * 2.5f - smoothTarget;
                if (toB.magnitude < 14) look += toB * 0.12f;
            }

            cineBlend = Mathf.MoveTowards(cineBlend, cine ? 1 : 0, dt * (cine ? 3f : 2f));
            float e = cineBlend * cineBlend * (3 - 2 * cineBlend);
            pos = Vector3.Lerp(pos, cinePos, e);
            look = Vector3.Lerp(look, cineLook, e);
            // 電子部品の柱にカメラがめりこまないようにする
            pos = World.CamClip(look, pos);

            fovPunch = Mathf.Lerp(fovPunch, 0, dt * 6);
            Cam.fieldOfView = Mathf.Lerp(BaseFov, cineFov, e) + fovPunch;

            shake = Mathf.MoveTowards(shake, 0, dt * 1.6f);
            float s = shake * shake;
            var sh = new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 32, 1) - 0.5f, Mathf.PerlinNoise(2, Time.unscaledTime * 32) - 0.5f, 0) * s * 1.6f;

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos);
            transform.position += transform.rotation * sh;
            transform.rotation *= Quaternion.Euler(0, 0, (Mathf.PerlinNoise(Time.unscaledTime * 20, 5) - 0.5f) * s * 8);
        }
    }
}
