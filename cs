import os
import json
import hmac
import hashlib
import asyncio
import threading
from urllib.parse import unquote, parse_qs

from fastapi import FastAPI, HTTPException
from fastapi.responses import HTMLResponse, JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from aiogram import Bot, Dispatcher, types, F
from aiogram.filters import CommandStart, Command
from aiogram.types import (
InlineKeyboardMarkup, InlineKeyboardButton,
WebAppInfo, ReplyKeyboardMarkup, KeyboardButton
)

# ── ENV ────────────────────────────────────────────────

BOT_TOKEN = os.getenv(“BOT_TOKEN”, “”)
WEBAPP_URL = os.getenv(“WEBAPP_URL”, “https://your-domain.com”)
PORT = int(os.getenv(“PORT”, 8000))

# ── FASTAPI ────────────────────────────────────────────

app = FastAPI(title=“CS2 Training Hub”)

app.add_middleware(
CORSMiddleware,
allow_origins=[”*”],
allow_methods=[”*”],
allow_headers=[”*”],
)

user_progress: dict[int, dict] = {}

class ProgressPayload(BaseModel):
init_data: str
completed_ids: list[int]
total_xp: int

def validate_init_data(init_data: str) -> dict | None:
try:
parsed = parse_qs(unquote(init_data))
data_dict = {k: v[0] for k, v in parsed.items()}
received_hash = data_dict.pop(“hash”, None)
if not received_hash:
return None
data_check_string = “\n”.join(
f”{k}={v}” for k, v in sorted(data_dict.items())
)
secret_key = hmac.new(
b”WebAppData”, BOT_TOKEN.encode(), hashlib.sha256
).digest()
expected = hmac.new(
secret_key, data_check_string.encode(), hashlib.sha256
).hexdigest()
if not hmac.compare_digest(expected, received_hash):
return None
return json.loads(data_dict.get(“user”, “{}”))
except Exception:
return None

@app.get(”/webapp”, response_class=HTMLResponse)
async def webapp():
with open(“static/index.html”, “r”, encoding=“utf-8”) as f:
return f.read()

@app.post(”/api/save-progress”)
async def save_progress(payload: ProgressPayload):
user = validate_init_data(payload.init_data)
if not user and BOT_TOKEN:
raise HTTPException(status_code=403, detail=“Invalid init data”)
if not user:
user = {“id”: 0, “first_name”: “Dev”}
uid = user.get(“id”, 0)
user_progress[uid] = {
“completed_ids”: payload.completed_ids,
“total_xp”: payload.total_xp,
“name”: user.get(“first_name”, “Player”),
}
return JSONResponse({“ok”: True})

@app.get(”/api/load-progress”)
async def load_progress(init_data: str = “”):
user = validate_init_data(init_data)
if not user and BOT_TOKEN:
raise HTTPException(status_code=403, detail=“Invalid init data”)
if not user:
user = {“id”: 0}
uid = user.get(“id”, 0)
data = user_progress.get(uid, {“completed_ids”: [], “total_xp”: 0})
return JSONResponse(data)

@app.get(”/health”)
async def health():
return {“status”: “ok”}

# ── AIOGRAM BOT ────────────────────────────────────────

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

def main_keyboard():
return ReplyKeyboardMarkup(
keyboard=[[KeyboardButton(
text=“🎮 Открыть Training Hub”,
web_app=WebAppInfo(url=f”{WEBAPP_URL}/webapp”)
)]],
resize_keyboard=True,
persistent=True
)

@dp.message(CommandStart())
async def cmd_start(message: types.Message):
await message.answer(
f”💥 <b>CS2 TRAINING HUB</b>\n\n”
f”Привет, <b>{message.from_user.first_name}</b>!\n\n”
f”Прокачай свой скилл в CS2:\n\n”
f”🎯 <b>АИМ</b> — точность и реакция\n”
f”💣 <b>ГРАНАТЫ</b> — смоки, флешки\n”
f”🗺 <b>КАРТЫ</b> — позиции и углы\n”
f”♟️ <b>ТАКТИКИ</b> — командная игра\n\n”
f”Нажми кнопку внизу 👇”,
reply_markup=main_keyboard(),
parse_mode=“HTML”
)

@dp.message(Command(“help”))
async def cmd_help(message: types.Message):
await message.answer(
“📖 <b>Команды:</b>\n\n”
“/start — перезапустить\n”
“/help — помощь\n\n”
“Нажми <b>🎮 Открыть Training Hub</b> для начала!”,
parse_mode=“HTML”
)

@dp.message(F.web_app_data)
async def handle_webapp_data(message: types.Message):
try:
data = json.loads(message.web_app_data.data)
action = data.get(“action”)
if action == “complete_training”:
name = data.get(“training”, “тренировку”)
xp = data.get(“xp”, 0)
await message.answer(
f”✅ <b>Тренировка завершена!</b>\n\n”
f”📋 {name}\n”
f”⚡ Получено: <b>+{xp} XP</b>\n\n”
f”Продолжай! 🔥”,
parse_mode=“HTML”
)
except Exception as e:
print(f”WebApp data error: {e}”)

@dp.message()
async def echo(message: types.Message):
await message.answer(
“Нажми кнопку ниже 👇”,
reply_markup=main_keyboard()
)

# ── STARTUP: запуск бота в отдельном потоке ────────────

def run_bot():
“”“Run aiogram bot in a separate thread with its own event loop.”””
loop = asyncio.new_event_loop()
asyncio.set_event_loop(loop)
loop.run_until_complete(dp.start_polling(bot))

@app.on_event(“startup”)
async def startup_event():
print(f”🌐 FastAPI запущен на порту {PORT}”)
print(f”🤖 Запускаю Telegram бота…”)
thread = threading.Thread(target=run_bot, daemon=True)
thread.start()
print(“✅ Бот запущен в фоне!”)

# ── ENTRY POINT ────────────────────────────────────────

if **name** == “**main**”:
import uvicorn
uvicorn.run(app, host=“0.0.0.0”, port=PORT)
