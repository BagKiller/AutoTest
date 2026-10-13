import json
import os
import sys
import uuid
from openai import OpenAI
from zhipuai import ZhipuAI


def _config_dir():
    if getattr(sys, 'frozen', False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))


CONFIG_FILE = os.path.join(_config_dir(), 'models.json')


class ModelManager:
    """模型配置的增删改查，持久化到 models.json"""

    def __init__(self):
        self.models: list[dict] = []
        self.active_id: str = ""
        self._load()

    def _load(self):
        if os.path.exists(CONFIG_FILE):
            try:
                with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                self.models = data.get('models', [])
                self.active_id = data.get('active_id', '')
            except Exception:
                self.models = []
                self.active_id = ''

        if not self.models:
            self.models = [
                {"id": "deepseek-official", "name": "DeepSeek-V3 (官方)",
                 "provider": "openai", "model": "deepseek-chat",
                 "api_key": "", "base_url": "https://api.deepseek.com"},
            ]
            self.active_id = self.models[0]['id']
            self._save()

        if not self.active_id or not self.get_model(self.active_id):
            self.active_id = self.models[0]['id']
            self._save()

    def _save(self):
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            json.dump({'models': self.models, 'active_id': self.active_id},
                      f, ensure_ascii=False, indent=2)

    def list_models(self) -> list[dict]:
        safe = []
        for m in self.models:
            safe.append({
                "id": m["id"], "name": m["name"],
                "provider": m["provider"], "model": m["model"],
                "base_url": m.get("base_url", ""),
                "has_key": bool(m.get("api_key")),
            })
        return safe

    def get_active(self) -> dict:
        return self.get_model(self.active_id) or self.models[0]

    def get_model(self, model_id: str) -> dict | None:
        for m in self.models:
            if m['id'] == model_id:
                return m
        return None

    def set_active(self, model_id: str) -> bool:
        if self.get_model(model_id):
            self.active_id = model_id
            self._save()
            return True
        return False

    def add_model(self, name: str, provider: str, model: str,
                  api_key: str, base_url: str = '') -> dict:
        m = {
            "id": str(uuid.uuid4())[:8],
            "name": name, "provider": provider,
            "model": model, "api_key": api_key, "base_url": base_url,
        }
        self.models.append(m)
        self._save()
        return m

    def update_model(self, model_id: str, **kwargs) -> bool:
        m = self.get_model(model_id)
        if not m:
            return False
        for key in ('name', 'provider', 'model', 'api_key', 'base_url'):
            if key in kwargs and kwargs[key]:
                m[key] = kwargs[key]
        self._save()
        return True

    def delete_model(self, model_id: str) -> bool:
        if len(self.models) <= 1:
            return False
        before = len(self.models)
        self.models = [m for m in self.models if m['id'] != model_id]
        if len(self.models) < before:
            if self.active_id == model_id:
                self.active_id = self.models[0]['id']
            self._save()
            return True
        return False

    def build_client(self, model_id: str = None):
        cfg = self.get_model(model_id) if model_id else self.get_active()
        if not cfg:
            cfg = self.models[0]

        if cfg['provider'] == 'zhipu':
            return ZhipuAI(api_key=cfg['api_key']), cfg['model']
        elif cfg['provider'] == 'gemini':
            return OpenAI(
                api_key=cfg['api_key'],
                base_url="https://generativelanguage.googleapis.com/v1beta/openai/",
            ), cfg['model']
        else:
            kwargs = {"api_key": cfg['api_key']}
            if cfg.get('base_url'):
                kwargs['base_url'] = cfg['base_url']
            return OpenAI(**kwargs), cfg['model']
