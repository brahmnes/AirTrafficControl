FROM python:3.6.4
WORKDIR /usr/src/app
EXPOSE 5000
RUN  pip install --upgrade pip \
     && pip install flask \
     && pip install flask-socketio \
     && pip install gevent \
     && pip install gevent-websocket \
     && pip install requests

# Set PYTHONUNBUFFERED to 0 we see console output asap
ENV PYTHONUNBUFFERED=0
ENV FLASK_APP=./main.py
COPY ./frontend ./
CMD ["flask", "run", "-h", "0.0.0.0"]