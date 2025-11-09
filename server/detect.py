#!/usr/bin/env python3
import io
import cv2
import time
import numpy as np
from PIL import Image
from flask import Flask, request, send_file, jsonify
from ultralytics import YOLO
from PIL import Image

# -----------------------------------------------------------------------------
# Flask app
# -----------------------------------------------------------------------------
app = Flask(__name__)
model = YOLO("yolo11n-seg.pt")
np.random.seed(42)  # reproducibility

# -----------------------------------------------------------------------------
# Health check
# -----------------------------------------------------------------------------
@app.route('/health', methods=['GET'])
def health():
    return jsonify({'status': 'ok'})

def getGazePixel(img):
    red_mask = cv2.inRange(img, (0,0,215), (40,40,255))
    ys, xs = np.where(red_mask)
    tmp = load_image_from_mask(img, red_mask)
    # cv2.imwrite("red_mask.jpg", tmp)
    if xs.size:
        gaze_x = int(xs.mean())
        gaze_y = int(ys.mean())
    else:
        return None, None
    print("gaze_x:", gaze_x, " gaze_y", gaze_x)

    gaze_x_pixel = min(max(gaze_x, 0), 640 - 1)
    gaze_y_pixel = min(max(gaze_y, 0), 640 - 1)

    dot_radius = 5
    cv2.circle(img, (gaze_x_pixel, gaze_y_pixel), dot_radius, (0, 0, 255), thickness=-1)
    # cv2.imwrite("image_with_gaze_dot.jpg", img)
    return gaze_x_pixel, gaze_y_pixel

def mask_from_poly(poly_xy, h, w):
    """poly_xy: list of [x_norm, y_norm] (0-1); returns uint8 mask"""
    pts = np.array([[int(x*w), int(y*h)] for x,y in poly_xy], dtype=np.int32)
    mask = np.zeros((h, w), np.uint8)
    cv2.fillPoly(mask, [pts], 1)
    return mask

def run_object_detection(model, image):
    results = model(image)
    detections = []
    for result in results:
        if result.masks is None:
            continue
        # result.save("segmentation.jpg")
        for box, cls_id, conf, poly in zip(result.boxes.xyxy.cpu().numpy(),
                                       result.boxes.cls.cpu().numpy(),
                                       result.boxes.conf.cpu().numpy(),
                                       result.masks.xyn):
            if float(conf) > 0.15:
                detections.append({
                    "bboxXYXY": box.tolist(),
                    "classIdx": int(cls_id),
                    "confidence": float(conf),
                    "mask": mask_from_poly(poly, 640, 640)
                })
    return detections

#input: gaze coordiantes, detections from detection model and predefined labels
#output: label index targeted by the gaze
def findTarget(gaze_x, gaze_y, detections, labels):
    
    # iterate through detections and if 
    # the object lies in our gaze position, denoted by a 1 bit
    # print the label of the object and return its correspond idx in detections
    for idx, detection in enumerate(detections):
        if(detection["mask"][gaze_y][gaze_x] == 1) :
            print("target: " + labels[detection["classIdx"]])
            return idx
        
    return -1


#input: image from headset, mask of target object
#output: cropped image around the target object
def load_image_from_mask(img, mask):
  
    # get x,y coords of where our object is  
    ys, xs = np.where(mask == 1)
    if xs.size == 0 or ys.size == 0:
        return None
    # get the corner x,y coords
    x1, x2 = xs.min(), xs.max()
    y1, y2 = ys.min(), ys.max()
    #get a cropped image based on our corner x,y coords
    crop_img = img[y1:y2+1, x1:x2+1]

    return crop_img
    
#input: detections from detection model, target label index and image from the headset
#output: cropped image with transparent pixels
def makeTransparent(detections, target_idx, img):
    #convert mask to cropped size
    mask = detections[target_idx]["mask"]
    ys, xs = np.where(mask == 1)
    y1, y2 = ys.min(), ys.max()
    x1, x2 = xs.min(), xs.max()
    cropped_mask = mask[y1:y2+1, x1:x2+1]
    
    # turn mask into 3 channels and formatted as uint8 for cv2
    mask_3ch = np.stack([cropped_mask*255, cropped_mask*255, cropped_mask*255], axis=-1).astype(np.uint8)
    
    # get rid of pixels in img that is not in mask
    res = cv2.bitwise_and(img, mask_3ch)
    
    return res


@app.route('/objectDetection', methods=['POST'])
def objectDetection():
    
    real_start = time.time()
    
    if not request.data:
        return jsonify({'error': 'No image data received'}), 405

    expected_byte_size = 640 * 640 * 3
    if len(request.data) != expected_byte_size:
        return jsonify({'error': 'Incorrect image size'}), 405
    
    image_array = np.frombuffer(request.data, dtype=np.uint8).reshape((640,640,3))
    img = image_array[:,:,::-1].copy() # bgr conversion
    img = img[::-1, :, :] #flip
    img = np.ascontiguousarray(img)
    # cv2.imwrite("ML_debug.png", img)
    image = Image.fromarray(image_array[::-1, :, :].copy()) # for yolo (flipped but not bgr)

    gaze_x, gaze_y = getGazePixel(img.copy())
    if gaze_x is None or gaze_y is None:
        return jsonify({'error': 'No gaze pixel found'}), 409

    labels = ["person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
				   "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
				   "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase",
				   "frisbee",
				   "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard",
				   "surfboard",
				   "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
				   "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
				   "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard",
				   "cell phone",
				   "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors",
				   "teddy bear",
				   "hair drier", "toothbrush"]
    start = time.time()
    

    #dummy variables
    detections =  []
    target_idx = []
    res = []
  
    #TODO: Step 1
    detections = run_object_detection(model, image)
    
    if not detections or len(detections) == 0:
        return jsonify({"err": "no object"}), 210
    modelsTime = time.time() - start

    start = time.time()
    #TODO: Step 2
    target_idx = findTarget(gaze_x, gaze_y, detections, labels)
    if target_idx == (None, None) or target_idx is None or target_idx == -1:
        return jsonify({'error': 'No target found'}), 401

    print("TARGET_IDX:", target_idx)
    #TODO: Step 3a
    target_crop = load_image_from_mask(img, detections[target_idx]["mask"])
    #TODO: Step 3b
    res = makeTransparent(detections, target_idx, target_crop)

    cv2.imwrite("result.png", res)
    ok, buf = cv2.imencode('.png', res, [cv2.IMWRITE_PNG_COMPRESSION, 1])
    total = time.time() - real_start
    print(f"Total = {total}s")
    print(f"Object detection = {modelsTime/total*100}%")

    if not ok:
        return jsonify({'error': 'encode failed'}), 503
    return send_file(io.BytesIO(buf.tobytes()), mimetype='image/png')

# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------
if __name__ == '__main__':
    # debug=False for faster prints; set to True if you want stacktraces
    app.run(host='0.0.0.0', port=5000, debug=False)
