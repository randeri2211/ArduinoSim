FROM python:3.12-slim

RUN useradd -m -u 1000 sandbox
USER sandbox
WORKDIR /home/sandbox

COPY --chown=sandbox:sandbox worker.py .

CMD ["python", "-u", "worker.py"]
