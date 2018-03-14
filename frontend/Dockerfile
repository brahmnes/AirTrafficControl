FROM tiangolo/uwsgi-nginx-flask:python3.6

RUN  pip install flask \
     && pip install flask-socketio \
     && pip install requests

ADD  . /app