FROM r8.im/pixray/text2image
RUN apt update && apt install libgl1-mesa-glx
RUN git clone https://github.com/pixray/diffvg
RUN cd diffvg; git submodule update --init --recursive && CMAKE_PREFIX_PATH=$(pyenv prefix) DIFFVG_CUDA=1 python setup.py install
RUN pip install basicsr
RUN pip install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu113