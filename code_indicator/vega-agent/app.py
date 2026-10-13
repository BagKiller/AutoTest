"""
VegaAgent 桌面应用入口
Flask 提供后端 API，pywebview 提供原生桌面窗口
"""
import os
import sys
import json
import threading
import socket
from flask import Flask, render_template, jsonify, request, Response, stream_with_context
from agent_core import VegaAgent
from model_manager import ModelManager
from tools.vega_runtime import vega_ping
from tools.system_ops import get_system_info
from tools.azure_devops import fetch_new_work_items
from tools.mcp_client import get_mcp_client


def resource_path(relative_path):
    if getattr(sys, 'frozen', False):
        base = sys._MEIPASS
    else:
        base = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base, relative_path)


app = Flask(
    __name__,
    template_folder=resource_path('templates'),
    static_folder=resource_path('static')
)
app.config['JSON_AS_ASCII'] = False

# 启动时从 config.json 加载 workspace_root
_cfg_path = resource_path('config.json')
try:
    with open(_cfg_path, 'r', encoding='utf-8') as _f:
        _startup_cfg = json.load(_f)
    _ws = _startup_cfg.get('vega_workspace_root', '')
    if _ws:
        os.environ['VEGA_WORKSPACE_ROOT'] = _ws
except Exception:
    pass

model_manager = ModelManager()
agent = VegaAgent(model_manager)


@app.route('/')
def index():
    return render_template('index.html')


@app.route('/api/chat', methods=['POST'])
def chat():
    """兼容旧接口：收集全部步骤后一次性返回"""
    data = request.get_json()
    message = data.get('message', '').strip()
    if not message:
        return jsonify({"steps": [{"type": "error", "content": "消息不能为空"}]})
    steps = agent.chat_collect(message)
    return jsonify({"steps": steps})


@app.route('/api/chat/stream', methods=['POST'])
def chat_stream():
    """SSE 流式端点：逐步推送每个处理阶段"""
    data = request.get_json()
    message = data.get('message', '').strip()

    def generate():
        if not message:
            yield f"data: {json.dumps({'type': 'error', 'content': '消息不能为空'}, ensure_ascii=False)}\n\n"
        else:
            for step in agent.chat(message):
                yield f"data: {json.dumps(step, ensure_ascii=False)}\n\n"
        yield f"data: {json.dumps({'type': 'done'})}\n\n"

    return Response(
        stream_with_context(generate()),
        mimetype='text/event-stream',
        headers={
            'Cache-Control': 'no-cache',
            'X-Accel-Buffering': 'no',
            'Connection': 'keep-alive',
        },
    )


@app.route('/api/clear', methods=['POST'])
def clear():
    agent.clear_history()
    return jsonify({"status": "ok"})


@app.route('/api/vega/ping')
def api_vega_ping():
    return jsonify(vega_ping())


@app.route('/api/system/info')
def api_system_info():
    return jsonify(get_system_info())


# ==========================================
# Azure DevOps 工作项 API
# ==========================================

@app.route('/api/workitems/new')
def api_workitems_new():
    """获取新建未分配的工作项列表"""
    return jsonify(fetch_new_work_items())


# ==========================================
# 配置 API
# ==========================================

def _load_config():
    config_path = resource_path('config.json')
    try:
        with open(config_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception:
        return {}


def _save_config(cfg):
    config_path = resource_path('config.json')
    with open(config_path, 'w', encoding='utf-8') as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)


@app.route('/api/config/workspace')
def get_workspace():
    cfg = _load_config()
    return jsonify({"workspace_root": cfg.get("vega_workspace_root", "")})


@app.route('/api/config/workspace', methods=['POST'])
def set_workspace():
    data = request.get_json()
    workspace_root = data.get('workspace_root', '').strip()
    if workspace_root and not os.path.isdir(workspace_root):
        return jsonify({"status": "error", "message": f"路径不存在: {workspace_root}"}), 400

    cfg = _load_config()
    cfg['vega_workspace_root'] = workspace_root
    _save_config(cfg)

    os.environ['VEGA_WORKSPACE_ROOT'] = workspace_root
    mcp = get_mcp_client()
    mcp.stop()

    return jsonify({"status": "ok"})


# ==========================================
# 模型管理 API
# ==========================================

@app.route('/api/models')
def list_models():
    return jsonify({
        "models": model_manager.list_models(),
        "active_id": model_manager.active_id,
    })


@app.route('/api/models/switch', methods=['POST'])
def switch_model():
    model_id = request.get_json().get('id', '')
    if agent.switch_model(model_id):
        agent.clear_history()
        return jsonify({"status": "ok"})
    return jsonify({"status": "error", "message": "模型不存在"}), 400


@app.route('/api/models/add', methods=['POST'])
def add_model():
    d = request.get_json()
    name = d.get('name', '').strip()
    provider = d.get('provider', 'openai').strip()
    model = d.get('model', '').strip()
    api_key = d.get('api_key', '').strip()
    base_url = d.get('base_url', '').strip()
    if not name or not model or not api_key:
        return jsonify({"status": "error", "message": "名称、模型和 API Key 不能为空"}), 400
    m = model_manager.add_model(name, provider, model, api_key, base_url)
    return jsonify({"status": "ok", "model": {"id": m["id"], "name": m["name"]}})


@app.route('/api/models/update', methods=['POST'])
def update_model():
    d = request.get_json()
    model_id = d.get('id', '')
    ok = model_manager.update_model(
        model_id,
        name=d.get('name', '').strip(),
        provider=d.get('provider', '').strip(),
        model=d.get('model', '').strip(),
        api_key=d.get('api_key', '').strip(),
        base_url=d.get('base_url', '').strip(),
    )
    if ok:
        agent._rebuild_client()
        return jsonify({"status": "ok"})
    return jsonify({"status": "error", "message": "模型不存在"}), 400


@app.route('/api/models/delete', methods=['POST'])
def delete_model():
    model_id = request.get_json().get('id', '')
    if model_manager.delete_model(model_id):
        agent._rebuild_client()
        return jsonify({"status": "ok"})
    return jsonify({"status": "error", "message": "无法删除（至少保留一个模型）"}), 400


# ==========================================
# 启动
# ==========================================

def find_free_port():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(('127.0.0.1', 0))
        return s.getsockname()[1]


def start_flask(port):
    app.run(host='127.0.0.1', port=port, debug=False, use_reloader=False)


def main():
    port = find_free_port()
    url = f'http://127.0.0.1:{port}'

    flask_thread = threading.Thread(target=start_flask, args=(port,), daemon=True)
    flask_thread.start()

    try:
        import webview
        print(f"[VegaAgent] 正在启动桌面窗口... ({url})")
        webview.create_window(
            'VegaAgent - Vega 智能助手',
            url,
            width=1200,
            height=800,
            min_size=(900, 600),
            text_select=True,
        )
        webview.start()
    except ImportError:
        import webbrowser
        print(f"[VegaAgent] pywebview 未安装，使用浏览器打开: {url}")
        print(f"[VegaAgent] 如需桌面窗口体验，请运行: pip install pywebview")
        webbrowser.open(url)
        try:
            flask_thread.join()
        except KeyboardInterrupt:
            print("\n[VegaAgent] 已退出")


if __name__ == '__main__':
    main()
