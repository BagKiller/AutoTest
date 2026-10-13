"""
测试 Gemini API 连接。
密钥从环境变量 GEMINI_API_KEY 读取；没有时再用本机 models.json（该文件不入库）。
"""
import json
import os
from pathlib import Path

import google.generativeai as genai


def load_api_key() -> str:
    key = os.environ.get("GEMINI_API_KEY", "").strip()
    if key:
        return key

    path = Path(__file__).with_name("models.json")
    if path.exists():
        data = json.loads(path.read_text(encoding="utf-8"))
        for item in data.get("models", []):
            if str(item.get("provider", "")).lower() == "gemini" and item.get("api_key"):
                return str(item["api_key"]).strip()

    raise SystemExit("请设置环境变量 GEMINI_API_KEY，或在本机 models.json 里配置 Gemini 的 api_key")


genai.configure(api_key=load_api_key())

try:
    print("正在检测 Gemini API 连接...")
    model = genai.GenerativeModel("models/gemini-2.0-flash-exp")

    # 发送测试消息
    response = model.generate_content("hi")

    print("✅ 检测结果：Gemini API 可用，网络通路正常！")
    print(f"服务器回复内容: {response.text}")

except Exception as e:
    print("❌ 检测结果：API 不可用。")
    print(f"具体报错类型: {type(e).__name__}")
    print(f"报错详细信息: {e}")
