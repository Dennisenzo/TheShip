The Ship needs a language model to run: the crew's words and the reading of what you type come from it.
Drop one or more GGUF chat/instruct models in this folder (gitignored). The game uses
gemma-4-E4B-it-Q4_K_M.gguf if it's here, otherwise the first *.gguf alphabetically, unless
you pick one with:  TheShip.exe --llm <file.gguf>   (--gpu-layers 0 = CPU only)

Same models as Cities:
  Gemma 4 E4B-it (Apache-2.0, 5.0 GB):
  https://huggingface.co/unsloth/gemma-4-E4B-it-GGUF/resolve/main/gemma-4-E4B-it-Q4_K_M.gguf
  Qwen3.5-9B (Apache-2.0, 5.7 GB):
  https://huggingface.co/unsloth/Qwen3.5-9B-GGUF/resolve/main/Qwen3.5-9B-Q4_K_M.gguf

Without a model the game shows what is missing and does not start.
